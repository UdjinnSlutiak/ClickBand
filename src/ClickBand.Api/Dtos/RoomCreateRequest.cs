using System.ComponentModel.DataAnnotations;

namespace ClickBand.Api.Dtos;

public sealed class RoomCreateRequest
{
    [Range(40, 320)]
    public int? TempoBpm { get; set; }

    [RegularExpression(@"^\d+/\d+$")]
    public string? TimeSignature { get; set; }

    [MaxLength(64)]
    public string? RequestedBy { get; set; }
}
