using System.Collections.Generic;
using System.Linq;
using ClickBand.Api.Dtos;
using ClickBand.Api.Models;
using ClickBand.Api.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClickBand.Api.Services;

public interface IRoomService
{
    Task<RoomDetails> CreateRoomAsync(RoomCreateRequest request, CancellationToken cancellationToken);
    Task<RoomDetails?> GetRoomAsync(string roomId, CancellationToken cancellationToken);
    Task<RoomState> ScheduleMetronomeStartAsync(string roomId, CancellationToken cancellationToken);
    Task<RoomState> StopMetronomeAsync(string roomId, CancellationToken cancellationToken);
    Task<RoomState> ChangeTempoAsync(string roomId, int tempoBpm, CancellationToken cancellationToken);
    Task<RoomState> ChangeTimeSignatureAsync(string roomId, string timeSignature, CancellationToken cancellationToken);
    Task<RoomParticipant> UpdateParticipantInstrumentAsync(string roomId, string clientId, string instrumentId, string displayName, CancellationToken cancellationToken);
    Task<RoomState?> CloseRoomAsync(string roomId, CancellationToken cancellationToken);
    Task<RoomParticipant> UpsertParticipantAsync(string roomId, RoomParticipant participant, CancellationToken cancellationToken);
    Task RemoveParticipantAsync(string roomId, string clientId, CancellationToken cancellationToken);
    Task RecordClientOffsetAsync(string roomId, string clientId, double offsetMs, CancellationToken cancellationToken);
}

public sealed class RoomService : IRoomService
{
    private readonly IRoomRepository _repository;
    private readonly IClock _clock;
    private readonly RoomOptions _roomOptions;
    private readonly SynchronizationOptions _syncOptions;
    private readonly ILogger<RoomService> _logger;

    public RoomService(
        IRoomRepository repository,
        IClock clock,
        IOptions<RoomOptions> roomOptions,
        IOptions<SynchronizationOptions> syncOptions,
        ILogger<RoomService> logger)
    {
        _repository = repository;
        _clock = clock;
        _roomOptions = roomOptions.Value;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task<RoomDetails> CreateRoomAsync(RoomCreateRequest request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var roomId = Guid.NewGuid().ToString("N");

        var tempo = request.TempoBpm ?? _roomOptions.DefaultTempoBpm;
        tempo = Math.Clamp(tempo, 40, 320);
        var timeSignature = string.IsNullOrWhiteSpace(request.TimeSignature)
            ? _roomOptions.DefaultTimeSignature
            : request.TimeSignature!;

        if (!TimeSignatureIsValid(timeSignature))
        {
            _logger.LogWarning("Invalid time signature {Signature} provided, falling back to default {DefaultSignature}", request.TimeSignature, _roomOptions.DefaultTimeSignature);
            timeSignature = _roomOptions.DefaultTimeSignature;
        }

        _logger.LogInformation("Generating roomId for requester {Requester}", request.RequestedBy);

        var state = new RoomState
        {
            RoomId = roomId,
            TempoBpm = tempo,
            TimeSignature = timeSignature,
            Status = RoomMetronomeStatus.Stopped,
            CreatedAt = now,
            LastUpdatedAt = now,
            CreatedBy = request.RequestedBy
        };

        _logger.LogInformation("Saving room {RoomId} with tempo {Tempo} signature {Signature}", roomId, tempo, timeSignature);

        await _repository.SaveRoomAsync(state, _roomOptions.Ttl, cancellationToken);
        _logger.LogInformation("Room {RoomId} persisted to Redis", roomId);
        return new RoomDetails(state, Array.Empty<RoomParticipant>());
    }

    public async Task<RoomDetails?> GetRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        var state = await _repository.GetRoomAsync(roomId, cancellationToken);
        if (state is null)
        {
            return null;
        }

        var participants = await _repository.GetParticipantsAsync(roomId, cancellationToken);
        return new RoomDetails(state, participants);
    }

    public Task<RoomState> ScheduleMetronomeStartAsync(string roomId, CancellationToken cancellationToken)
    {
        return UpdateRoomAsync(roomId, state =>
        {
            var scheduleAt = _clock.UtcNow.AddMilliseconds(_syncOptions.LeadTimeMs);
            return state with
            {
                Status = RoomMetronomeStatus.Running,
                ScheduledStartAt = scheduleAt,
                LastServerBeatTimestamp = scheduleAt
            };
        }, cancellationToken);
    }

    public Task<RoomState> StopMetronomeAsync(string roomId, CancellationToken cancellationToken)
    {
        return UpdateRoomAsync(roomId, state =>
        {
            return state with
            {
                Status = RoomMetronomeStatus.Stopped,
                ScheduledStartAt = null
            };
        }, cancellationToken);
    }

    public Task<RoomState> ChangeTempoAsync(string roomId, int tempoBpm, CancellationToken cancellationToken)
    {
        var normalizedTempo = Math.Clamp(tempoBpm, 40, 320);
        return UpdateRoomAsync(roomId, state => state with { TempoBpm = normalizedTempo }, cancellationToken);
    }

    public Task<RoomState> ChangeTimeSignatureAsync(string roomId, string timeSignature, CancellationToken cancellationToken)
    {
        if (!TimeSignatureIsValid(timeSignature))
        {
            throw new InvalidOperationException("Invalid time signature format.");
        }

        var normalized = timeSignature.Trim();
        return UpdateRoomAsync(roomId, state => state with { TimeSignature = normalized }, cancellationToken);
    }

    public async Task<RoomState?> CloseRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        var state = await _repository.GetRoomAsync(roomId, cancellationToken);
        if (state is null)
        {
            return null;
        }

        await _repository.DeleteRoomAsync(roomId, cancellationToken);
        return state;
    }

    public async Task<RoomParticipant> UpsertParticipantAsync(string roomId, RoomParticipant participant, CancellationToken cancellationToken)
    {
        var room = await _repository.GetRoomAsync(roomId, cancellationToken)
                   ?? throw new InvalidOperationException("Room not found");

        var participants = await _repository.GetParticipantsAsync(roomId, cancellationToken);
        if (participants.All(p => p.ClientId != participant.ClientId) &&
            participants.Count >= _roomOptions.MaxParticipants)
        {
            throw new InvalidOperationException("Room is full");
        }

        var normalized = participant with
        {
            RoomId = room.RoomId,
            JoinedAt = participant.JoinedAt == default ? _clock.UtcNow : participant.JoinedAt
        };

        await _repository.UpsertParticipantAsync(normalized, _roomOptions.Ttl, cancellationToken);
        return normalized;
    }

    public Task RemoveParticipantAsync(string roomId, string clientId, CancellationToken cancellationToken)
    {
        return _repository.RemoveParticipantAsync(roomId, clientId, cancellationToken);
    }

    public Task RecordClientOffsetAsync(string roomId, string clientId, double offsetMs, CancellationToken cancellationToken)
    {
        return _repository.SaveClockOffsetAsync(roomId, clientId, offsetMs, _roomOptions.Ttl, cancellationToken);
    }

    public async Task<RoomParticipant> UpdateParticipantInstrumentAsync(string roomId, string clientId, string instrumentId, string displayName, CancellationToken cancellationToken)
    {
        var participant = await _repository.GetParticipantAsync(roomId, clientId, cancellationToken)
                           ?? throw new InvalidOperationException("Participant not found");

        var capabilities = participant.Capabilities is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(participant.Capabilities);

        capabilities["instrument"] = instrumentId;

        var updated = participant with
        {
            DisplayName = displayName,
            Capabilities = capabilities
        };

        await _repository.UpsertParticipantAsync(updated, _roomOptions.Ttl, cancellationToken);
        return updated;
    }

    private async Task<RoomState> UpdateRoomAsync(string roomId, Func<RoomState, RoomState> update, CancellationToken cancellationToken)
    {
        var current = await _repository.GetRoomAsync(roomId, cancellationToken)
                      ?? throw new InvalidOperationException("Room not found");

        var updated = update(current) with
        {
            LastUpdatedAt = _clock.UtcNow
        };

        await _repository.SaveRoomAsync(updated, _roomOptions.Ttl, cancellationToken);
        return updated;
    }

    private static bool TimeSignatureIsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0], out var numerator) && numerator > 0
               && int.TryParse(parts[1], out var denominator) && denominator > 0;
    }
}
