namespace ClickBand.Api.Dtos;

public record ParticipantDto(string ClientId, string DisplayName, DateTimeOffset JoinedAt)
{
    public string? InstrumentId { get; init; }
}
