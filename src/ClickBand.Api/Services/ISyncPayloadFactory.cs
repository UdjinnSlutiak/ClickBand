using ClickBand.Api.Models;
using ClickBand.Api.Options;
using Microsoft.Extensions.Options;

namespace ClickBand.Api.Services;

public interface ISyncPayloadFactory
{
    MetronomeSyncPayload Create(RoomState state);
}

public sealed class SyncPayloadFactory : ISyncPayloadFactory
{
    private readonly IClock _clock;
    private readonly SynchronizationOptions _options;

    public SyncPayloadFactory(IClock clock, IOptions<SynchronizationOptions> options)
    {
        _clock = clock;
        _options = options.Value;
    }

    public MetronomeSyncPayload Create(RoomState state)
    {
        var now = _clock.UtcNow;
        var startAt = state.ScheduledStartAt ?? now.AddMilliseconds(_options.LeadTimeMs);

        return new MetronomeSyncPayload
        {
            RoomId = state.RoomId,
            TempoBpm = state.TempoBpm,
            BeatIntervalMs = state.BeatIntervalMs,
            ServerTimestampUtc = now,
            StartAtUtc = startAt,
            TimeSignature = state.TimeSignature
        };
    }
}
