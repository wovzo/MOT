using System;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MOT.Application.Analytics.Queries;

namespace MOT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableCors("AllowAll")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(IMediator mediator, ILogger<AnalyticsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var summary = await _mediator.Send(new GetFocusAnalyticsSummaryQuery());
                return Ok(summary);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { Error = "User is not authenticated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving focus analytics summary.");
                return StatusCode(500, new { Error = "An unexpected error occurred while retrieving focus analytics summary." });
            }
        }

        [HttpGet("daily")]
        public async Task<IActionResult> GetDaily([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var query = new GetDailyAnalyticsQuery
                {
                    StartDate = startDate,
                    EndDate = endDate
                };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (FluentValidation.ValidationException ex)
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
                _logger.LogError(ex, "An unexpected error occurred while retrieving daily focus analytics.");
                return StatusCode(500, new { Error = "An unexpected error occurred while retrieving daily focus analytics." });
            }
        }

        [HttpGet("weekly")]
        public async Task<IActionResult> GetWeekly([FromQuery] int weeks = 4)
        {
            try
            {
                var query = new GetWeeklyAnalyticsQuery
                {
                    Weeks = weeks
                };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (FluentValidation.ValidationException ex)
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
                _logger.LogError(ex, "An unexpected error occurred while retrieving weekly focus analytics.");
                return StatusCode(500, new { Error = "An unexpected error occurred while retrieving weekly focus analytics." });
            }
        }
    }
}
