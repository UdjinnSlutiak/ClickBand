using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ClickBand.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private const int MaxBodyLengthToLog = 256 * 1024; // 256 KB

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? bodyText = null;
        if (context.Request.Body.CanRead &&
            context.Request.ContentLength is > 0 &&
            context.Request.ContentLength <= MaxBodyLengthToLog &&
            IsTextBasedContentType(context.Request.ContentType))
        {
            context.Request.EnableBuffering();
            context.Request.Body.Seek(0, SeekOrigin.Begin);

            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            bodyText = await reader.ReadToEndAsync();
            context.Request.Body.Seek(0, SeekOrigin.Begin);
        }

        _logger.LogInformation(
            "Incoming {Method} {Path}{Query} BodyLength={BodyLength}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Request.ContentLength);

        if (!string.IsNullOrWhiteSpace(bodyText))
        {
            _logger.LogDebug("Request body: {Body}", bodyText);
        }

        await _next(context);
    }

    private static bool IsTextBasedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        if (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
