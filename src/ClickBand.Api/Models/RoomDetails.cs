namespace ClickBand.Api.Models;

public record RoomDetails(RoomState State, IReadOnlyCollection<RoomParticipant> Participants);
