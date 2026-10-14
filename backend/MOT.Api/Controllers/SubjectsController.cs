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
using MOT.Application.Common.Exceptions;
using MOT.Application.Subjects.Commands;
using MOT.Application.Subjects.Queries;

namespace MOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableCors("AllowAll")]
public class SubjectsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SubjectsController> _logger;

    public SubjectsController(IMediator mediator, ILogger<SubjectsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubjects()
    {
        try
        {
            var subjects = await _mediator.Send(new GetSubjectsQuery());
            return Ok(subjects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetSubjects");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while retrieving subjects." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectCommand command)
    {
        try
        {
            var created = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetSubjectById), new { id = created.Id }, created);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Errors.First().ErrorMessage });
        }
        catch (DuplicateSubjectNameException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in CreateSubject");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while creating the subject." });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSubjectById(Guid id)
    {
        try
        {
            var subject = await _mediator.Send(new GetSubjectByIdQuery(id));
            return Ok(subject);
        }
        catch (SubjectNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetSubjectById for {SubjectId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while retrieving the subject." });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] UpdateSubjectCommand command)
    {
        try
        {
            command.Id = id;
            var updated = await _mediator.Send(command);
            return Ok(updated);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Errors.First().ErrorMessage });
        }
        catch (DuplicateSubjectNameException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (SubjectNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in UpdateSubject for {SubjectId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while updating the subject." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubject(Guid id)
    {
        try
        {
            await _mediator.Send(new DeleteSubjectCommand(id));
            return NoContent();
        }
        catch (SubjectNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in DeleteSubject for {SubjectId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while deleting the subject." });
        }
    }
}
