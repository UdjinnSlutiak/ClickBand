namespace ClickBand.Api.Options;

public sealed class RoomOptions
{
    public int DefaultTempoBpm { get; set; } = 120;
    public string DefaultTimeSignature { get; set; } = "4/4";
    public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(6);
    public int MaxParticipants { get; set; } = 32;
}
