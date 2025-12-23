using System.Collections.Generic;

namespace ClickBand.Api.Models;

public record RoomParticipant
{
    public required string RoomId { get; init; }
    public required string ClientId { get; init; }
    public string DisplayName { get; init; } = "Guest";
    public DateTimeOffset JoinedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string>? Capabilities { get; init; }
}
