namespace ClickBand.Api.Models;

public record RoomState
{
    public string RoomId { get; init; } = string.Empty;
    public int TempoBpm { get; init; } = 120;
    public string TimeSignature { get; init; } = "4/4";
    public RoomMetronomeStatus Status { get; init; } = RoomMetronomeStatus.Stopped;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastServerBeatTimestamp { get; init; }
    public DateTimeOffset? ScheduledStartAt { get; init; }
    public string? CreatedBy { get; init; }
    public double BeatIntervalMs => 60000d / Math.Max(1, TempoBpm);
}
