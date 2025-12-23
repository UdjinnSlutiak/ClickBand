namespace ClickBand.Api.Models;

public record ClockSyncResponse
{
    public required DateTimeOffset ServerTimestampUtc { get; init; }
    public required int MaxDriftMs { get; init; }
}
