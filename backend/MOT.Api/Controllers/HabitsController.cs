using System;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MOT.Application.Common.Exceptions;
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
        private readonly ILogger<HabitsController> _logger;

        public HabitsController(IMediator mediator, ILogger<HabitsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetHabits()
        {
            try
            {
                var result = await _mediator.Send(new GetHabitsQuery());
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving habits.");
                return StatusCode(500, new { Error = "An unexpected error occurred while retrieving habits." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateHabit([FromBody] CreateHabitCommand command)
        {
            try
            {
                var habitId = await _mediator.Send(command);
                return Ok(new { Id = habitId });
            }
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating habit.");
                return StatusCode(500, new { Error = "An unexpected error occurred while creating habit." });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHabitById(Guid id)
        {
            try
            {
                var habit = await _mediator.Send(new GetHabitByIdQuery(id));
                return Ok(habit);
            }
            catch (HabitNotFoundException ex)
            {
                return NotFound(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving habit {HabitId}.", id);
                return StatusCode(500, new { Error = "An unexpected error occurred while retrieving habit." });
            }
        }

        [HttpPost("{id}/toggle")]
        public async Task<IActionResult> ToggleHabit(Guid id)
        {
            try
            {
                var isCompletedNow = await _mediator.Send(new ToggleHabitCompletionCommand { HabitId = id });
                return Ok(new { IsCompletedToday = isCompletedNow });
            }
            catch (HabitNotFoundException ex)
            {
                return NotFound(new { Error = ex.Message });
            }
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while toggling habit {HabitId}.", id);
                return StatusCode(500, new { Error = "An unexpected error occurred while toggling habit." });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHabit(Guid id, [FromBody] UpdateHabitCommand command)
        {
            try
            {
                command.Id = id;
                var updated = await _mediator.Send(command);
                return Ok(updated);
            }
            catch (HabitNotFoundException ex)
            {
                return NotFound(new { Error = ex.Message });
            }
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating habit {HabitId}.", id);
                return StatusCode(500, new { Error = "An unexpected error occurred while updating habit." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHabit(Guid id)
        {
            try
            {
                await _mediator.Send(new DeleteHabitCommand(id));
                return NoContent();
            }
            catch (HabitNotFoundException ex)
            {
                return NotFound(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting habit {HabitId}.", id);
                return StatusCode(500, new { Error = "An unexpected error occurred while deleting habit." });
            }
        }
    }
}
