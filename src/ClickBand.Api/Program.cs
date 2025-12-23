using ClickBand.Api.Hubs;
using ClickBand.Api.Options;
using ClickBand.Api.Services;
using ClickBand.Api.Serialization;
using ClickBand.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Telegram.Bot;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions();
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<SynchronizationOptions>(builder.Configuration.GetSection("Synchronization"));
builder.Services.Configure<RoomOptions>(builder.Configuration.GetSection("Rooms"));

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = builder.Configuration["REDIS_CONNECTION_STRING"]
                        ?? builder.Configuration.GetConnectionString("Redis")
                        ?? "clickband-redis:6379";

    var options = ConfigurationOptions.Parse(configuration);
    options.AbortOnConnectFail = false;

    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Redis");
    logger.LogInformation("Connecting to Redis at {Endpoint}", configuration);

    return ConnectionMultiplexer.Connect(options);
});

builder.Services.AddSingleton<IRoomRepository, RedisRoomRepository>();
builder.Services.AddSingleton<IRoomLinkBuilder, RoomLinkBuilder>();
builder.Services.AddSingleton<ISyncPayloadFactory, SyncPayloadFactory>();
builder.Services.AddSingleton<IRoomService, RoomService>();
builder.Services.AddSingleton<ITelegramWebhookService, TelegramWebhookService>();

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.BotToken))
    {
        throw new InvalidOperationException("Telegram bot token is not configured.");
    }

    return new TelegramBotClient(options.BotToken);
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(SnakeCaseEnumNamingPolicy.Instance));
        options.JsonSerializerOptions.Converters.Add(new UnixDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new UnixDateTimeOffsetConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("ModelState");
            foreach (var kvp in context.ModelState)
            {
                foreach (var error in kvp.Value.Errors)
                {
                    logger.LogWarning("Model binding error for {Key}: {Error}", kvp.Key, error.ErrorMessage);
                }
            }

            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request validation failed"
            };
            return new BadRequestObjectResult(problem);
        };
    });

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddCheck<RedisHealthCheck>("redis");

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Frontend:AllowedOrigins")
            .Get<string[]>();

        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .SetIsOriginAllowed(_ => true);
        }
    });
});

var httpsSection = builder.Configuration.GetSection("HttpsRedirection");
var enforceHttps = httpsSection.GetValue<bool>("Enabled");
if (enforceHttps)
{
    builder.Services.AddHttpsRedirection(options =>
    {
        var httpsPort = httpsSection.GetValue<int?>("HttpsPort");
        if (httpsPort.HasValue)
        {
            options.HttpsPort = httpsPort.Value;
        }
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseCors("frontend");
app.UseRouting();
app.UseAuthorization();
if (enforceHttps)
{
    app.UseHttpsRedirection();
}

app.MapControllers();
app.MapHub<RoomsHub>("/hubs/rooms");
app.MapHealthChecks("/health");

app.Run();
