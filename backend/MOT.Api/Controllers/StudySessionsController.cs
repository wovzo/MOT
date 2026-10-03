using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MOT.Application.Common.Exceptions;
using MOT.Application.StudySessions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MOT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableCors("AllowAll")]
    public class StudySessionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<StudySessionsController> _logger;

        public StudySessionsController(IMediator mediator, ILogger<StudySessionsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetStudySessions()
        {
            try
            {
                var sessions = await _mediator.Send(new GetStudySessionsQuery());
                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving study sessions.");
                return StatusCode(500, new { Error = "An unexpected error occurred while retrieving study sessions." });
            }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveStudySession()
        {
            try
            {
                var session = await _mediator.Send(new GetActiveStudySessionQuery());
                if (session == null)
                {
                    return NoContent();
                }
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving active study session.");
                return StatusCode(500, new { Error = "An unexpected error occurred while retrieving active study session." });
            }
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartSession([FromBody] StartStudySessionCommand command)
        {
            try
            {
                var session = await _mediator.Send(command);
                return Ok(session);
            }
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { Error = errors.FirstOrDefault() ?? "Validation failed.", Errors = errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while starting study session.");
                return StatusCode(500, new { Error = "An unexpected error occurred while starting study session." });
            }
        }

        [HttpPost("{id}/end")]
        public async Task<IActionResult> EndSession(Guid id)
        {
            try
            {
                var session = await _mediator.Send(new EndStudySessionCommand { SessionId = id });
                return Ok(session);
            }
            catch (StudySessionNotFoundException ex)
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
                _logger.LogError(ex, "An unexpected error occurred while ending study session {SessionId}.", id);
                return StatusCode(500, new { Error = "An unexpected error occurred while ending study session." });
            }
        }
    }
}
