using System.Collections.Concurrent;
using ClickBand.Api.Dtos;
using ClickBand.Api.Models;
using ClickBand.Api.Options;
using ClickBand.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ClickBand.Api.Hubs;

public sealed class RoomsHub : Hub
{
    private static readonly ConcurrentDictionary<string, (string RoomId, string ClientId)> Connections = new();

    private readonly IRoomService _roomService;
    private readonly ISyncPayloadFactory _syncPayloadFactory;
    private readonly SynchronizationOptions _syncOptions;
    private readonly ILogger<RoomsHub> _logger;
    private readonly IRoomLinkBuilder _linkBuilder;

    public RoomsHub(
        IRoomService roomService,
        ISyncPayloadFactory syncPayloadFactory,
        IOptions<SynchronizationOptions> syncOptions,
        IRoomLinkBuilder linkBuilder,
        ILogger<RoomsHub> logger)
    {
        _roomService = roomService;
        _syncPayloadFactory = syncPayloadFactory;
        _syncOptions = syncOptions.Value;
        _linkBuilder = linkBuilder;
        _logger = logger;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Connections.TryRemove(Context.ConnectionId, out var info))
        {
            await _roomService.RemoveParticipantAsync(info.RoomId, info.ClientId, Context.ConnectionAborted);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(info.RoomId));
            await Clients.Group(RoomGroup(info.RoomId))
                .SendAsync("ParticipantLeft", info.ClientId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinRoom(string roomId, ClientJoinRequest joinRequest)
    {
        var participant = new RoomParticipant
        {
            RoomId = roomId,
            ClientId = joinRequest.ClientId,
            DisplayName = string.IsNullOrWhiteSpace(joinRequest.DisplayName) ? "Guest" : joinRequest.DisplayName,
            Capabilities = joinRequest.Capabilities?.Metadata
        };

        RoomParticipant savedParticipant;
        try
        {
            savedParticipant = await _roomService.UpsertParticipantAsync(roomId, participant, Context.ConnectionAborted);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Join room {RoomId} failed", roomId);
            throw new HubException(ex.Message);
        }
        Connections[Context.ConnectionId] = (roomId, savedParticipant.ClientId);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));

        var room = await _roomService.GetRoomAsync(roomId, Context.ConnectionAborted)
                   ?? throw new HubException("Room not found");

        await Clients.Caller.SendAsync("RoomSnapshot", new
        {
            room = room.State,
            participants = room.Participants,
            inviteUrl = _linkBuilder.BuildRoomUrl(room.State.RoomId)
        });

        await Clients.OthersInGroup(RoomGroup(roomId))
            .SendAsync("ParticipantJoined", new ParticipantDto(savedParticipant.ClientId, savedParticipant.DisplayName, savedParticipant.JoinedAt)
            {
                InstrumentId = savedParticipant.Capabilities is not null &&
                               savedParticipant.Capabilities.TryGetValue("instrument", out var instrument)
                    ? instrument
                    : null
            });

        _logger.LogInformation("Client {ClientId} joined room {RoomId}", savedParticipant.ClientId, roomId);
    }

    public async Task LeaveRoom(string roomId)
    {
        if (Connections.TryRemove(Context.ConnectionId, out var info))
        {
            await _roomService.RemoveParticipantAsync(info.RoomId, info.ClientId, Context.ConnectionAborted);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(info.RoomId));

            await Clients.Group(RoomGroup(info.RoomId))
                .SendAsync("ParticipantLeft", info.ClientId);
        }
    }

    public async Task RequestMetronomeStart(string roomId)
    {
        RoomState state;
        try
        {
            state = await _roomService.ScheduleMetronomeStartAsync(roomId, Context.ConnectionAborted);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
        var payload = _syncPayloadFactory.Create(state);
        await Clients.Group(RoomGroup(roomId)).SendAsync("MetronomeStart", payload);
    }

    public async Task RequestMetronomeStop(string roomId)
    {
        RoomState state;
        try
        {
            state = await _roomService.StopMetronomeAsync(roomId, Context.ConnectionAborted);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
        await Clients.Group(RoomGroup(roomId)).SendAsync("MetronomeStop", state);
    }

    public async Task RequestTempoChange(string roomId, int tempoBpm)
    {
        RoomState state;
        try
        {
            state = await _roomService.ChangeTempoAsync(roomId, tempoBpm, Context.ConnectionAborted);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
        await Clients.Group(RoomGroup(roomId)).SendAsync("TempoChanged", state);
    }

    public async Task RequestTimeSignatureChange(string roomId, string timeSignature)
    {
        RoomState state;
        try
        {
            state = await _roomService.ChangeTimeSignatureAsync(roomId, timeSignature, Context.ConnectionAborted);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
        await Clients.Group(RoomGroup(roomId)).SendAsync("TimeSignatureChanged", state);
    }

    public async Task SetInstrument(string instrumentId, string displayName)
    {
        if (!Connections.TryGetValue(Context.ConnectionId, out var info))
        {
            throw new HubException("Client not joined to a room");
        }

        RoomParticipant participant;
        try
        {
            participant = await _roomService.UpdateParticipantInstrumentAsync(info.RoomId, info.ClientId, instrumentId, displayName, Context.ConnectionAborted);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }

        await Clients.Group(RoomGroup(info.RoomId)).SendAsync("ParticipantUpdated", new ParticipantDto(participant.ClientId, participant.DisplayName, participant.JoinedAt)
        {
            InstrumentId = instrumentId
        });
    }

    public async Task<ClockSyncResponse> PingServer(string roomId, string clientId, long clientSentTimestampMs)
    {
        var serverTime = DateTimeOffset.UtcNow;
        var offset = serverTime.ToUnixTimeMilliseconds() - clientSentTimestampMs;
        await _roomService.RecordClientOffsetAsync(roomId, clientId, offset, Context.ConnectionAborted);

        return new ClockSyncResponse
        {
            ServerTimestampUtc = serverTime,
            MaxDriftMs = _syncOptions.MaxDriftMs
        };
    }

    private static string RoomGroup(string roomId) => $"room:{roomId}";
}
