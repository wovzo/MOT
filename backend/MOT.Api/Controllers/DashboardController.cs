using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MOT.Application.Common.Exceptions;
using MOT.Application.Dashboard.Queries;

namespace MOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableCors("AllowAll")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IMediator mediator, ILogger<DashboardController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboardSummary([FromQuery] int? timeZoneOffsetMinutes)
    {
        try
        {
            var summary = await _mediator.Send(new GetDashboardSummaryQuery
            {
                TimeZoneOffsetMinutes = timeZoneOffsetMinutes
            });
            return Ok(summary);
        }
        catch (UserNotFoundException ex)
        {
            _logger.LogWarning(ex, "User not found while getting dashboard summary");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving dashboard summary");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while retrieving dashboard summary." });
        }
    }
}
