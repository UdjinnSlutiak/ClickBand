namespace ClickBand.Api.Options;

public sealed class SynchronizationOptions
{
    public int MaxDriftMs { get; set; } = 3;
    public int PingSampleSize { get; set; } = 5;
    public int LeadTimeMs { get; set; } = 1500;
    public int HeartbeatIntervalMs { get; set; } = 2000;
}
