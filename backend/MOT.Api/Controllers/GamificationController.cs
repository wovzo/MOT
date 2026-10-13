using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MOT.Application.Common.Exceptions;
using MOT.Application.Gamification.Queries;

namespace MOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableCors("AllowAll")]
public class GamificationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<GamificationController> _logger;

    public GamificationController(IMediator mediator, ILogger<GamificationController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _mediator.Send(new GetGamificationSummaryQuery(), cancellationToken);
            return Ok(summary);
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { Error = "User is not authenticated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while retrieving gamification summary.");
            return StatusCode(500, new { Error = "An unexpected error occurred while retrieving gamification summary." });
        }
    }

    [HttpGet("achievements")]
    public async Task<IActionResult> GetAchievements(CancellationToken cancellationToken)
    {
        try
        {
            var achievements = await _mediator.Send(new GetAchievementsQuery(), cancellationToken);
            return Ok(achievements);
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { Error = "User is not authenticated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while retrieving achievements.");
            return StatusCode(500, new { Error = "An unexpected error occurred while retrieving achievements." });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetXpHistoryQuery { Limit = limit };
            var history = await _mediator.Send(query, cancellationToken);
            return Ok(history);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
        }
        catch (UserNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { Error = "User is not authenticated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while retrieving XP history.");
            return StatusCode(500, new { Error = "An unexpected error occurred while retrieving XP history." });
        }
    }
}
