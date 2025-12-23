using System.Linq;
using ClickBand.Api.Models;

namespace ClickBand.Api.Dtos;

public record RoomResponse
{
    public required string RoomId { get; init; }
    public required int TempoBpm { get; init; }
    public required string TimeSignature { get; init; }
    public required RoomMetronomeStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastUpdatedAt { get; init; }
    public DateTimeOffset? ScheduledStartAt { get; init; }
    public DateTimeOffset? LastServerBeatTimestamp { get; init; }
    public string? CreatedBy { get; init; }
    public required string InviteUrl { get; init; }
    public required IReadOnlyCollection<ParticipantDto> Participants { get; init; }

    public static RoomResponse FromRoomDetails(RoomDetails details, string inviteUrl)
    {
        return new RoomResponse
        {
            RoomId = details.State.RoomId,
            TempoBpm = details.State.TempoBpm,
            TimeSignature = details.State.TimeSignature,
            Status = details.State.Status,
            CreatedAt = details.State.CreatedAt,
            LastUpdatedAt = details.State.LastUpdatedAt,
            ScheduledStartAt = details.State.ScheduledStartAt,
            LastServerBeatTimestamp = details.State.LastServerBeatTimestamp,
            CreatedBy = details.State.CreatedBy,
            InviteUrl = inviteUrl,
            Participants = details.Participants
                .Select(p => new ParticipantDto(p.ClientId, p.DisplayName, p.JoinedAt)
                {
                    InstrumentId = TryGetInstrument(p)
                })
                .ToArray()
        };
    }

    private static string? TryGetInstrument(RoomParticipant participant)
    {
        if (participant.Capabilities is null)
        {
            return null;
        }

        return participant.Capabilities.TryGetValue("instrument", out var instrument)
            ? instrument
            : null;
    }
}
