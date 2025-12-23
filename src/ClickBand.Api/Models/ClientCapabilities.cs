namespace ClickBand.Api.Models;

public record ClientCapabilities
{
    public bool SupportsWebAudio { get; init; }
    public bool SupportsHaptics { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
