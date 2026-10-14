using System;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MOT.Application.DailyGoals.Commands;
using MOT.Application.DailyGoals.Queries;

namespace MOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableCors("AllowAll")]
public class DailyGoalsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DailyGoalsController> _logger;

    public DailyGoalsController(IMediator mediator, ILogger<DailyGoalsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress([FromQuery] string? date, [FromQuery] int? timeZoneOffsetMinutes)
    {
        try
        {
            var query = new GetDailyGoalProgressQuery
            {
                Date = date,
                TimeZoneOffsetMinutes = timeZoneOffsetMinutes
            };

            var progress = await _mediator.Send(query);
            return Ok(progress);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Errors.First().ErrorMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetProgress");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while retrieving daily goal progress." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> SetDailyGoal([FromBody] SetDailyGoalCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Errors.First().ErrorMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SetDailyGoal");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while saving the daily goal." });
        }
    }
}
