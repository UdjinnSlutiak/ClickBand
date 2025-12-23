namespace ClickBand.Api.Models;

public record MetronomeSyncPayload
{
    public required string RoomId { get; init; }
    public required int TempoBpm { get; init; }
    public required double BeatIntervalMs { get; init; }
    public required DateTimeOffset ServerTimestampUtc { get; init; }
    public required DateTimeOffset StartAtUtc { get; init; }
    public string TimeSignature { get; init; } = "4/4";
}
