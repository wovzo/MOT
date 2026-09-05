using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using MOT.Application.Tasks.Commands;
using MOT.Application.Tasks.Queries;

namespace MOT.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableCors("AllowAll")]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdStr, out var userId))
            return userId;
        throw new UnauthorizedAccessException("Invalid token");
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks()
    {
        var query = new GetTasksQuery(GetUserId());
        var tasks = await _mediator.Send(query);
        return Ok(tasks);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        var command = new CreateTaskCommand(GetUserId(), request.Title, request.Description);
        var taskId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetTasks), new { id = taskId }, new { id = taskId });
    }

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> ToggleTask(Guid id)
    {
        var command = new ToggleTaskCommand(id, GetUserId());
        var success = await _mediator.Send(command);

        if (!success)
            return NotFound(new { error = "Task not found" });

        return Ok(new { success = true });
    }
}

public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
