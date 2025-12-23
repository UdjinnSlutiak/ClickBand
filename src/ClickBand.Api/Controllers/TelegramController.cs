using ClickBand.Api.Options;
using ClickBand.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace ClickBand.Api.Controllers;

[Route("api/telegram/webhook")]
public sealed class TelegramController : ControllerBase
{
    private readonly ITelegramWebhookService _telegramWebhook;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        ITelegramWebhookService telegramWebhook,
        IOptions<TelegramOptions> options,
        ILogger<TelegramController> logger)
    {
        _telegramWebhook = telegramWebhook;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Update? update, CancellationToken cancellationToken)
    {
        if (update is null)
        {
            _logger.LogWarning("Received null update payload.");
            return BadRequest("Update payload is required.");
        }

        _logger.LogInformation("Received update from {Username}",
            update.Message?.From?.Username ?? update.ChannelPost?.From?.Username);
        
        if (!ValidateSecret())
        {
            return Unauthorized();
        }

        await _telegramWebhook.HandleUpdateAsync(update, cancellationToken);
        
        _logger.LogInformation("Successfully handled update from {Username}", 
            update.Message?.From?.Username ?? update.ChannelPost?.From?.Username);
        
        return Ok();
    }

    private bool ValidateSecret()
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return true;
        }

        if (!Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var header))
        {
            return false;
        }

        return string.Equals(_options.WebhookSecret, header.ToString(), StringComparison.Ordinal);
    }
}
