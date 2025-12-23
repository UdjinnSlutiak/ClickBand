using ClickBand.Api.Dtos;
using ClickBand.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ClickBand.Api.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly IRoomLinkBuilder _linkBuilder;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(IRoomService roomService, IRoomLinkBuilder linkBuilder, ILogger<RoomsController> logger)
    {
        _roomService = roomService;
        _linkBuilder = linkBuilder;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<RoomResponse>> CreateRoom([FromBody] RoomCreateRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Room creation requested tempo {Tempo} signature {Signature} from {Requester}",
            request.TempoBpm, request.TimeSignature, request.RequestedBy);

        var room = await _roomService.CreateRoomAsync(request, cancellationToken);
        _logger.LogInformation("Room {RoomId} created successfully", room.State.RoomId);

        var inviteUrl = _linkBuilder.BuildRoomUrl(room.State.RoomId);
        var response = RoomResponse.FromRoomDetails(room, inviteUrl);
        return CreatedAtAction(nameof(GetRoom), new { roomId = room.State.RoomId }, response);
    }

    [HttpGet("{roomId}")]
    public async Task<ActionResult<RoomResponse>> GetRoom(string roomId, CancellationToken cancellationToken)
    {
        var room = await _roomService.GetRoomAsync(roomId, cancellationToken);
        if (room is null)
        {
            return NotFound();
        }

        var inviteUrl = _linkBuilder.BuildRoomUrl(room.State.RoomId);
        return Ok(RoomResponse.FromRoomDetails(room, inviteUrl));
    }

    [HttpDelete("{roomId}")]
    public async Task<IActionResult> DeleteRoom(string roomId, CancellationToken cancellationToken)
    {
        var state = await _roomService.CloseRoomAsync(roomId, cancellationToken);
        return state is null ? NotFound() : NoContent();
    }
}
