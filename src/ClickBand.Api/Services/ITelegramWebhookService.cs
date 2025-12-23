using ClickBand.Api.Dtos;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ClickBand.Api.Services;

public interface ITelegramWebhookService
{
    Task HandleUpdateAsync(Update update, CancellationToken cancellationToken);
}

public sealed class TelegramWebhookService : ITelegramWebhookService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IRoomService _roomService;
    private readonly IRoomLinkBuilder _linkBuilder;
    private readonly ILogger<TelegramWebhookService> _logger;

    public TelegramWebhookService(
        ITelegramBotClient botClient,
        IRoomService roomService,
        IRoomLinkBuilder linkBuilder,
        ILogger<TelegramWebhookService> logger)
    {
        _botClient = botClient;
        _roomService = roomService;
        _linkBuilder = linkBuilder;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message ?? update.ChannelPost;
        
        if (message != null && !string.IsNullOrWhiteSpace(message.Text))
        {
            if (IsRoomRequest(message))
            {
                await HandleRoomCreationAsync(message, cancellationToken);
            }
        }
        else
            _logger.LogWarning("No message provided for {Username}", 
                update.Message?.From?.Username ?? update.ChannelPost?.From?.Username);
    }

    private async Task HandleRoomCreationAsync(Message message, CancellationToken cancellationToken)
    {
        var initiator = message.From?.Username ??
                        $"{message.From?.FirstName} {message.From?.LastName}".Trim();

        _logger.LogInformation("Telegram chat {ChatId} requested room creation", message.Chat.Id);

        var room = await _roomService.CreateRoomAsync(new RoomCreateRequest
        {
            RequestedBy = initiator
        }, cancellationToken);

        var inviteUrl = _linkBuilder.BuildRoomUrl(room.State.RoomId);
        var markdown = $"{EscapeMarkdownV2("Room created for this chat.")}";
        InlineKeyboardMarkup? keyboard = null;
        if (Uri.TryCreate(inviteUrl, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Open room", inviteUrl));
        }

        await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: markdown,
            parseMode: ParseMode.MarkdownV2,
            disableWebPagePreview: false,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Room {RoomId} created from Telegram chat {ChatId}", room.State.RoomId, message.Chat.Id);
    }

    private static bool IsRoomRequest(Message message)
    {
        var text = message.Text?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.StartsWith("/start", StringComparison.OrdinalIgnoreCase)
               || text.Contains("create room")
               || text.Contains("metronome")
               || text.Contains("clickband");
    }

    private static string EscapeMarkdownV2(string value)
    {
        var chars = new HashSet<char>(new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!', '\\' });
        var builder = new StringBuilder(value.Length * 2);
        foreach (var ch in value)
        {
            if (chars.Contains(ch))
            {
                builder.Append('\\');
            }
            builder.Append(ch);
        }
        return builder.ToString();
    }

    private static string FormatMarkdownLink(string text, string url)
    {
        return $"[{EscapeMarkdownV2(text)}]({EscapeMarkdownV2(url)})";
    }
}
