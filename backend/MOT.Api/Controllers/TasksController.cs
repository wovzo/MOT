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
using MOT.Application.Tasks.Commands;
using MOT.Application.Tasks.Queries;

namespace MOT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableCors("AllowAll")]
    public class TasksController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<TasksController> _logger;

        public TasksController(IMediator mediator, ILogger<TasksController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks()
        {
            try
            {
                var tasks = await _mediator.Send(new GetTasksQuery());
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetTasks");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while retrieving tasks." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskCommand command)
        {
            try
            {
                var taskId = await _mediator.Send(command);
                return CreatedAtAction(nameof(GetTasks), new { id = taskId }, new { id = taskId });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Errors.First().ErrorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CreateTask");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while creating the task." });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTaskById(Guid id)
        {
            try
            {
                var task = await _mediator.Send(new GetTaskByIdQuery(id));
                return Ok(task);
            }
            catch (TaskNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetTaskById for {TaskId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while retrieving the task." });
            }
        }

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleTask(Guid id)
        {
            try
            {
                var isCompleted = await _mediator.Send(new ToggleTaskCommand(id));
                return Ok(new { isCompleted });
            }
            catch (TaskNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ToggleTask");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while toggling the task." });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(Guid id, [FromBody] UpdateTaskCommand command)
        {
            try
            {
                command.Id = id;
                var updatedTask = await _mediator.Send(command);
                return Ok(updatedTask);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Errors.First().ErrorMessage });
            }
            catch (TaskNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in UpdateTask for {TaskId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while updating the task." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(Guid id)
        {
            try
            {
                await _mediator.Send(new DeleteTaskCommand(id));
                return NoContent();
            }
            catch (TaskNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in DeleteTask for {TaskId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while deleting the task." });
            }
        }
    }
}
