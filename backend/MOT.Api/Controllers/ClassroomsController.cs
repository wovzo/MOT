using System;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MOT.Application.Classrooms;
using MOT.Application.Common.Exceptions;

namespace MOT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableCors("AllowAll")]
    public class ClassroomsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ClassroomsController> _logger;

        public ClassroomsController(IMediator mediator, ILogger<ClassroomsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("{roomName}/participants")]
        public async Task<IActionResult> GetParticipants(string roomName)
        {
            try
            {
                var participants = await _mediator.Send(new GetRoomParticipantsQuery { RoomName = roomName });
                return Ok(participants);
            }
            catch (ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving participants for room {RoomName}.", roomName);
                return StatusCode(500, new { Error = "An unexpected error occurred while retrieving room participants." });
            }
        }

        [HttpPost("{roomName}/join")]
        public async Task<IActionResult> Join(string roomName, [FromBody] JoinRequest req)
        {
            try
            {
                var participant = await _mediator.Send(new JoinRoomCommand { RoomName = roomName, IsVideoOn = req.IsVideoOn });
                return Ok(participant);
            }
            catch (ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (ClassroomFullException ex)
            {
                return Conflict(new { Error = ex.Message });
            }
            catch (ClassroomSeatConflictException ex)
            {
                return Conflict(new { Error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Error = "User is not authenticated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error joining room {RoomName}.", roomName);
                return StatusCode(500, new { Error = "An unexpected error occurred while joining the classroom." });
            }
        }

        [HttpPost("{roomName}/ping")]
        public async Task<IActionResult> Ping(string roomName)
        {
            try
            {
                await _mediator.Send(new PingRoomCommand { RoomName = roomName });
                return Ok();
            }
            catch (ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Error = "User is not authenticated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error pinging room {RoomName}.", roomName);
                return StatusCode(500, new { Error = "An unexpected error occurred while pinging the classroom." });
            }
        }

        [HttpPost("{roomName}/leave")]
        public async Task<IActionResult> Leave(string roomName)
        {
            try
            {
                await _mediator.Send(new LeaveRoomCommand { RoomName = roomName });
                return Ok();
            }
            catch (ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Error = "User is not authenticated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error leaving room {RoomName}.", roomName);
                return StatusCode(500, new { Error = "An unexpected error occurred while leaving the classroom." });
            }
        }
    }

    public class JoinRequest
    {
        public bool IsVideoOn { get; set; }
    }
}
