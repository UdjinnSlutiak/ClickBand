using System.ComponentModel.DataAnnotations;
using ClickBand.Api.Models;

namespace ClickBand.Api.Dtos;

public sealed class ClientJoinRequest
{
    [Required]
    public string ClientId { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(64)]
    public string DisplayName { get; set; } = "Guest";

    public ClientCapabilities? Capabilities { get; set; }
}
