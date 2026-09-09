using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MOT.Application.Habits.Commands;
using MOT.Application.Habits.Queries;

namespace MOT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableCors("AllowAll")]
    public class HabitsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public HabitsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        private Guid GetUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid user token.");
            }
            return userId;
        }

        [HttpGet]
        public async Task<IActionResult> GetHabits()
        {
            try
            {
                var query = new GetHabitsQuery { UserId = GetUserId() };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateHabit([FromBody] CreateHabitRequest request)
        {
            try
            {
                var command = new CreateHabitCommand
                {
                    UserId = GetUserId(),
                    Title = request.Title,
                    Description = request.Description
                };
                
                var habitId = await _mediator.Send(command);
                return Ok(new { Id = habitId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("{id}/toggle")]
        public async Task<IActionResult> ToggleHabit(Guid id)
        {
            try
            {
                var command = new ToggleHabitCompletionCommand
                {
                    UserId = GetUserId(),
                    HabitId = id
                };
                
                var isCompletedNow = await _mediator.Send(command);
                return Ok(new { IsCompletedToday = isCompletedNow });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
    }

    public class CreateHabitRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
