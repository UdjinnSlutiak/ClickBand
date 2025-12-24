using ClickBand.Api.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace ClickBand.Api.Services;

public sealed class TelegramWebhookRegistrationService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConfiguration _configuration;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramWebhookRegistrationService> _logger;

    public TelegramWebhookRegistrationService(
        ITelegramBotClient botClient,
        IConfiguration configuration,
        IOptions<TelegramOptions> options,
        ILogger<TelegramWebhookRegistrationService> logger)
    {
        _botClient = botClient;
        _configuration = configuration;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var webhookUrl = _configuration["TELEGRAM_WEBHOOK_URL"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogInformation("Telegram webhook url not configured; skipping webhook registration.");
            return;
        }

        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out _))
        {
            _logger.LogWarning("Telegram webhook url is invalid: {WebhookUrl}", webhookUrl);
            return;
        }

        var secret = string.IsNullOrWhiteSpace(_options.WebhookSecret)
            ? null
            : _options.WebhookSecret;

        try
        {
            await _botClient.SetWebhookAsync(
                url: webhookUrl,
                secretToken: secret,
                cancellationToken: cancellationToken);
            _logger.LogInformation("Telegram webhook registered for {WebhookUrl}", webhookUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Telegram webhook for {WebhookUrl}", webhookUrl);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
