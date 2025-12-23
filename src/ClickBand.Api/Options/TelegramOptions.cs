namespace ClickBand.Api.Options;

public sealed class TelegramOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string BasePublicUrl { get; set; } = string.Empty;
    public string RoomPath { get; set; } = "/rooms";
}
