using System.Text.Json;
using System.Linq;
using ClickBand.Api.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ClickBand.Api.Services;

public interface IRoomRepository
{
    Task SaveRoomAsync(RoomState state, TimeSpan ttl, CancellationToken cancellationToken);
    Task<RoomState?> GetRoomAsync(string roomId, CancellationToken cancellationToken);
    Task DeleteRoomAsync(string roomId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RoomParticipant>> GetParticipantsAsync(string roomId, CancellationToken cancellationToken);
    Task<RoomParticipant?> GetParticipantAsync(string roomId, string clientId, CancellationToken cancellationToken);
    Task UpsertParticipantAsync(RoomParticipant participant, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveParticipantAsync(string roomId, string clientId, CancellationToken cancellationToken);
    Task SaveClockOffsetAsync(string roomId, string clientId, double offsetMs, TimeSpan ttl, CancellationToken cancellationToken);
}

public sealed class RedisRoomRepository : IRoomRepository
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisRoomRepository> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public RedisRoomRepository(IConnectionMultiplexer connection, ILogger<RedisRoomRepository> logger)
    {
        _db = connection.GetDatabase();
        _logger = logger;
    }

    public async Task SaveRoomAsync(RoomState state, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var key = RoomStateKey(state.RoomId);
        var payload = JsonSerializer.Serialize(state, SerializerOptions);
        _logger.LogDebug("Saving room key {Key} with TTL {Ttl}", key, ttl);
        await _db.StringSetAsync(key, payload, ttl);
        await _db.KeyExpireAsync(ParticipantsKey(state.RoomId), ttl);
    }

    public async Task<RoomState?> GetRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        var result = await _db.StringGetAsync(RoomStateKey(roomId));
        if (result.IsNullOrEmpty)
        {
            return null;
        }
        return JsonSerializer.Deserialize<RoomState>(result!, SerializerOptions);
    }

    public async Task DeleteRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        var keys = new RedisKey[]
        {
            RoomStateKey(roomId),
            ParticipantsKey(roomId)
        };
        await _db.KeyDeleteAsync(keys);
    }

    public async Task<IReadOnlyCollection<RoomParticipant>> GetParticipantsAsync(string roomId, CancellationToken cancellationToken)
    {
        var hashEntries = await _db.HashGetAllAsync(ParticipantsKey(roomId));
        if (hashEntries.Length == 0)
        {
            return Array.Empty<RoomParticipant>();
        }

        return hashEntries
            .Select(entry => JsonSerializer.Deserialize<RoomParticipant>(entry.Value!, SerializerOptions))
            .Where(participant => participant is not null)
            .Select(participant => participant!)
            .ToArray();
    }

    public async Task<RoomParticipant?> GetParticipantAsync(string roomId, string clientId, CancellationToken cancellationToken)
    {
        var entry = await _db.HashGetAsync(ParticipantsKey(roomId), clientId);
        if (entry.IsNullOrEmpty)
        {
            return null;
        }
        return JsonSerializer.Deserialize<RoomParticipant>(entry!, SerializerOptions);
    }

    public async Task UpsertParticipantAsync(RoomParticipant participant, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var key = ParticipantsKey(participant.RoomId);
        var payload = JsonSerializer.Serialize(participant, SerializerOptions);
        await _db.HashSetAsync(key, participant.ClientId, payload);
        await _db.KeyExpireAsync(key, ttl);
    }

    public Task RemoveParticipantAsync(string roomId, string clientId, CancellationToken cancellationToken)
    {
        return _db.HashDeleteAsync(ParticipantsKey(roomId), clientId);
    }

    public Task SaveClockOffsetAsync(string roomId, string clientId, double offsetMs, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var key = ClockOffsetKey(roomId, clientId);
        return _db.StringSetAsync(key, offsetMs, ttl);
    }

    private static string RoomStateKey(string roomId) => $"room:{roomId}:state";
    private static string ParticipantsKey(string roomId) => $"room:{roomId}:participants";
    private static string ClockOffsetKey(string roomId, string clientId) => $"room:{roomId}:client:{clientId}:offset";
}
