using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MOT.Application.Classrooms;

namespace MOT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableCors("AllowAll")]
    public class ClassroomsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ClassroomsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{roomName}/participants")]
        public async Task<IActionResult> GetParticipants(string roomName)
        {
            var participants = await _mediator.Send(new GetRoomParticipantsQuery { RoomName = roomName });
            return Ok(participants);
        }

        [HttpPost("{roomName}/join")]
        public async Task<IActionResult> Join(string roomName, [FromBody] JoinRequest req)
        {
            var participant = await _mediator.Send(new JoinRoomCommand { RoomName = roomName, IsVideoOn = req.IsVideoOn });
            return Ok(participant);
        }

        [HttpPost("{roomName}/ping")]
        public async Task<IActionResult> Ping(string roomName)
        {
            await _mediator.Send(new PingRoomCommand { RoomName = roomName });
            return Ok();
        }

        [HttpPost("{roomName}/leave")]
        public async Task<IActionResult> Leave(string roomName)
        {
            await _mediator.Send(new LeaveRoomCommand { RoomName = roomName });
            return Ok();
        }
    }

    public class JoinRequest
    {
        public bool IsVideoOn { get; set; }
    }
}
