using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MOT.Application.StudySessions;
using System;
using System.Threading.Tasks;

namespace MOT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StudySessionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StudySessionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetStudySessions()
        {
            var sessions = await _mediator.Send(new GetStudySessionsQuery());
            return Ok(sessions);
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartSession([FromBody] StartStudySessionCommand command)
        {
            var session = await _mediator.Send(command);
            return Ok(session);
        }

        [HttpPost("{id}/end")]
        public async Task<IActionResult> EndSession(Guid id)
        {
            var session = await _mediator.Send(new EndStudySessionCommand { SessionId = id });
            return Ok(session);
        }
    }
}
