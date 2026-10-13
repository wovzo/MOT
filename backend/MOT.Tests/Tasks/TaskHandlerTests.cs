using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Gamification;
using MOT.Application.Tasks.Commands;
using MOT.Application.Tasks.Queries;
using MOT.Domain.Entities;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Tasks;

public class TaskHandlerTests
{
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IGamificationService> _gamificationServiceMock;
    private readonly Mock<IAchievementService> _achievementServiceMock;
    private readonly List<DailyTask> _tasks;

    public TaskHandlerTests()
    {
        _tasks = new List<DailyTask>();
        _dbContextMock = new Mock<IAppDbContext>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _gamificationServiceMock = new Mock<IGamificationService>();
        _achievementServiceMock = new Mock<IAchievementService>();

        var mockTasksDbSet = MockDbSetHelper.CreateMockDbSet(_tasks);

        _dbContextMock.Setup(c => c.Tasks).Returns(mockTasksDbSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    // ==========================================
    // 1. Creation Tests
    // ==========================================

    [Fact]
    public async Task CreateTask_WhenAuthenticated_CreatesTaskForUserWithGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new CreateTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new CreateTaskCommand
        {
            Title = "Finish Homework",
            Description = "Math problem set 4"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        _tasks.Should().HaveCount(1);
        var createdTask = _tasks.Single();
        createdTask.Id.Should().Be(result);
        createdTask.UserId.Should().Be(userId);
        createdTask.Title.Should().Be("Finish Homework");
        createdTask.Description.Should().Be("Math problem set 4");
        createdTask.IsCompleted.Should().BeFalse();

        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-guid")]
    public async Task CreateTask_WhenUserIdIsMissingOrInvalid_ThrowsUnauthorizedAccessException(string? invalidUserId)
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);

        var handler = new CreateTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new CreateTaskCommand { Title = "Study Chemistry", Description = "Chapter 3" };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tasks.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CreateTaskCommand_HasNoClientUserIdProperty()
    {
        // Assert that client payload cannot supply UserId through CreateTaskCommand
        var properties = typeof(CreateTaskCommand).GetProperties();
        properties.Should().NotContain(p => p.Name.Equals("UserId", StringComparison.OrdinalIgnoreCase));
    }

    // ==========================================
    // 2. Validation Tests (CreateTaskCommandValidator)
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Validator_WhenTitleIsEmptyOrWhitespace_FailsValidation(string? emptyTitle)
    {
        // Arrange
        var validator = new CreateTaskCommandValidator();
        var command = new CreateTaskCommand { Title = emptyTitle! };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTaskCommand.Title));
    }

    [Fact]
    public void Validator_WhenTitleExceeds100Characters_FailsValidation()
    {
        // Arrange
        var validator = new CreateTaskCommandValidator();
        var command = new CreateTaskCommand { Title = new string('A', 101) };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTaskCommand.Title));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Complete P0.4")]
    public void Validator_WhenTitleIsValid_PassesValidation(string validTitle)
    {
        // Arrange
        var validator = new CreateTaskCommandValidator();
        var command = new CreateTaskCommand { Title = validTitle, Description = "Detailed description" };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WhenTitleIsExactly100Characters_PassesValidation()
    {
        // Arrange
        var validator = new CreateTaskCommandValidator();
        var command = new CreateTaskCommand { Title = new string('Z', 100) };

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    // ==========================================
    // 3. Listing & Isolation Tests
    // ==========================================

    [Fact]
    public async Task GetTasks_ReturnsOnlyAuthenticatedUserTasks_OrderedByCreatedAtDescending()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(currentUserId.ToString());

        var olderTask = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            Title = "Older Task",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            IsCompleted = false
        };
        var newerTask = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            Title = "Newer Task",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            IsCompleted = true
        };
        var otherUserTask = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            Title = "Other User Task",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };

        _tasks.AddRange(new[] { olderTask, newerTask, otherUserTask });

        var handler = new GetTasksQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetTasksQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(newerTask.Id);
        result[0].Title.Should().Be("Newer Task");
        result[0].IsCompleted.Should().BeTrue();

        result[1].Id.Should().Be(olderTask.Id);
        result[1].Title.Should().Be("Older Task");
        result[1].IsCompleted.Should().BeFalse();

        result.Should().NotContain(t => t.Id == otherUserTask.Id);
    }

    [Fact]
    public async Task GetTasks_WhenUserHasNoTasks_ReturnsEmptyList()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(currentUserId.ToString());

        var handler = new GetTasksQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetTasksQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad-guid")]
    public async Task GetTasks_WhenUserIdIsMissingOrInvalid_ThrowsUnauthorizedAccessException(string? invalidUserId)
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);
        var handler = new GetTasksQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetTasksQuery(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ==========================================
    // 4. Toggle Tests
    // ==========================================

    [Fact]
    public async Task ToggleTask_WhenIncomplete_MarksTaskCompletedAndReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Task to complete",
            IsCompleted = false
        };
        _tasks.Add(task);

        var handler = new ToggleTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object, _gamificationServiceMock.Object, _achievementServiceMock.Object);

        // Act
        var result = await handler.Handle(new ToggleTaskCommand(task.Id), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        task.IsCompleted.Should().BeTrue();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleTask_WhenCompleted_MarksTaskIncompleteAndReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Task to uncheck",
            IsCompleted = true
        };
        _tasks.Add(task);

        var handler = new ToggleTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object, _gamificationServiceMock.Object, _achievementServiceMock.Object);

        // Act
        var result = await handler.Handle(new ToggleTaskCommand(task.Id), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        task.IsCompleted.Should().BeFalse();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleTask_WhenTaskDoesNotExist_ThrowsTaskNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new ToggleTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object, _gamificationServiceMock.Object, _achievementServiceMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new ToggleTaskCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TaskNotFoundException>();
    }

    [Fact]
    public async Task ToggleTask_WhenTaskBelongsToAnotherUser_ThrowsTaskNotFoundException()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(currentUserId.ToString());

        var otherUserTask = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = anotherUserId,
            Title = "Private Task",
            IsCompleted = false
        };
        _tasks.Add(otherUserTask);

        var handler = new ToggleTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object, _gamificationServiceMock.Object, _achievementServiceMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new ToggleTaskCommand(otherUserTask.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TaskNotFoundException>();
        otherUserTask.IsCompleted.Should().BeFalse();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task ToggleTask_WhenUserIdIsMissingOrInvalid_ThrowsUnauthorizedAccessException(string? invalidUserId)
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);
        var handler = new ToggleTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object, _gamificationServiceMock.Object, _achievementServiceMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new ToggleTaskCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public void ToggleTaskCommand_HasNoClientUserIdProperty()
    {
        var properties = typeof(ToggleTaskCommand).GetProperties();
        properties.Should().NotContain(p => p.Name.Equals("UserId", StringComparison.OrdinalIgnoreCase));
    }

    // ==========================================
    // 5. Controller HTTP Semantics Tests
    // ==========================================

    [Fact]
    public async Task TasksController_GetTasks_Returns200WithTaskList()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<TasksController>>();

        var dtoList = new List<DailyTaskDto>
        {
            new DailyTaskDto(Guid.NewGuid(), "Task 1", "Desc 1", false, DateTime.UtcNow),
            new DailyTaskDto(Guid.NewGuid(), "Task 2", null, true, DateTime.UtcNow)
        };

        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtoList);

        var controller = new TasksController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetTasks();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(dtoList);
    }

    [Fact]
    public async Task TasksController_CreateTask_WhenValid_Returns201CreatedWithId()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<TasksController>>();
        var taskId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(taskId);

        var controller = new TasksController(mediatorMock.Object, loggerMock.Object);
        var command = new CreateTaskCommand { Title = "New Task", Description = "Notes" };

        // Act
        var result = await controller.CreateTask(command);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(taskId);
    }

    [Fact]
    public async Task TasksController_CreateTask_WhenValidationFails_Returns400BadRequest()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<TasksController>>();

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("Title", "Title is required.")
        };
        mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateTaskCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FluentValidation.ValidationException(validationFailures));

        var controller = new TasksController(mediatorMock.Object, loggerMock.Object);
        var command = new CreateTaskCommand { Title = "" };

        // Act
        var result = await controller.CreateTask(command);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);

        var errorProperty = badRequestResult.Value?.GetType().GetProperty("error")?.GetValue(badRequestResult.Value);
        errorProperty.Should().Be("Title is required.");
    }

    [Fact]
    public async Task TasksController_ToggleTask_WhenSuccessful_Returns200WithCompletionState()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<TasksController>>();
        var taskId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.Is<ToggleTaskCommand>(c => c.Id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new TasksController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.ToggleTask(taskId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);

        var isCompletedProp = okResult.Value?.GetType().GetProperty("isCompleted")?.GetValue(okResult.Value);
        isCompletedProp.Should().Be(true);
    }

    [Fact]
    public async Task TasksController_ToggleTask_WhenTaskNotFound_Returns404NotFound()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<TasksController>>();
        var taskId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<ToggleTaskCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskNotFoundException(taskId));

        var controller = new TasksController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.ToggleTask(taskId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);

        var errorProperty = notFoundResult.Value?.GetType().GetProperty("error")?.GetValue(notFoundResult.Value);
        errorProperty.Should().Be($"Task with ID '{taskId}' was not found.");
    }

    [Fact]
    public async Task TasksController_WhenUnexpectedExceptionOccurs_Returns500AndHidesInternalDetails()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<TasksController>>();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetTasksQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Sensitive MySQL syntax error details"));

        var controller = new TasksController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetTasks();

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);

        var errorProperty = statusResult.Value?.GetType().GetProperty("error")?.GetValue(statusResult.Value)?.ToString();
        errorProperty.Should().NotContain("MySQL");
        errorProperty.Should().NotContain("Sensitive");
        errorProperty.Should().Be("An unexpected error occurred while retrieving tasks.");

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    // ==========================================
    // Phase 3: Daily Task CRUD Tests
    // ==========================================

    [Fact]
    public async Task GetTaskById_WhenTaskExistsForUser_ReturnsTaskDto()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Read Biology",
            Description = "Chapter 4",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };
        _tasks.Add(task);

        var handler = new GetTaskByIdQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetTaskByIdQuery(task.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(task.Id);
        result.Title.Should().Be("Read Biology");
        result.Description.Should().Be("Chapter 4");
        result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetTaskById_WhenTaskDoesNotExist_ThrowsTaskNotFoundException()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new GetTaskByIdQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var nonExistentId = Guid.NewGuid();

        Func<Task> act = async () => await handler.Handle(new GetTaskByIdQuery(nonExistentId), CancellationToken.None);

        await act.Should().ThrowAsync<TaskNotFoundException>();
    }

    [Fact]
    public async Task GetTaskById_WhenTaskBelongsToDifferentUser_ThrowsTaskNotFoundException()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userA.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userB, // Belongs to User B
            Title = "User B Private Task"
        };
        _tasks.Add(task);

        var handler = new GetTaskByIdQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        Func<Task> act = async () => await handler.Handle(new GetTaskByIdQuery(task.Id), CancellationToken.None);

        // Security boundary: User A cannot read User B's task (returns 404, not 200)
        await act.Should().ThrowAsync<TaskNotFoundException>();
    }

    [Fact]
    public async Task UpdateTask_WhenValid_UpdatesFieldsAndPreservesCreatedAt()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var originalCreated = DateTime.UtcNow.AddDays(-2);

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Original Title",
            Description = "Original Desc",
            IsCompleted = false,
            CreatedAt = originalCreated
        };
        _tasks.Add(task);

        var handler = new UpdateTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new UpdateTaskCommand
        {
            Id = task.Id,
            Title = "Updated Title",
            Description = "Updated Desc",
            IsCompleted = true
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Title.Should().Be("Updated Title");
        result.Description.Should().Be("Updated Desc");
        result.IsCompleted.Should().BeTrue();
        result.CreatedAt.Should().Be(originalCreated);

        task.Title.Should().Be("Updated Title");
        task.Description.Should().Be("Updated Desc");
        task.IsCompleted.Should().BeTrue();
        task.CreatedAt.Should().Be(originalCreated);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTask_WhenTaskBelongsToDifferentUser_ThrowsTaskNotFoundException()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userA.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userB,
            Title = "User B Task",
            Description = "Secret"
        };
        _tasks.Add(task);

        var handler = new UpdateTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new UpdateTaskCommand
        {
            Id = task.Id,
            Title = "Hacked Title"
        };

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Security boundary: User A cannot modify User B's task
        await act.Should().ThrowAsync<TaskNotFoundException>();
        task.Title.Should().Be("User B Task"); // Unchanged
    }

    [Fact]
    public void UpdateTaskCommandValidator_ValidatesTitleLengthAndPresence()
    {
        var validator = new UpdateTaskCommandValidator();

        var validCommand = new UpdateTaskCommand
        {
            Id = Guid.NewGuid(),
            Title = "Valid Task",
            Description = "Short description"
        };
        validator.Validate(validCommand).IsValid.Should().BeTrue();

        var emptyTitleCommand = new UpdateTaskCommand
        {
            Id = Guid.NewGuid(),
            Title = ""
        };
        validator.Validate(emptyTitleCommand).IsValid.Should().BeFalse();

        var overlongTitleCommand = new UpdateTaskCommand
        {
            Id = Guid.NewGuid(),
            Title = new string('A', 101)
        };
        validator.Validate(overlongTitleCommand).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTask_WhenValid_RemovesTaskFromDb()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Task to delete"
        };
        _tasks.Add(task);

        var handler = new DeleteTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        await handler.Handle(new DeleteTaskCommand(task.Id), CancellationToken.None);

        _tasks.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTask_WhenTaskBelongsToDifferentUser_ThrowsTaskNotFoundExceptionAndDoesNotDelete()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userA.ToString());

        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userB,
            Title = "User B Task"
        };
        _tasks.Add(task);

        var handler = new DeleteTaskCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        Func<Task> act = async () => await handler.Handle(new DeleteTaskCommand(task.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TaskNotFoundException>();
        _tasks.Should().HaveCount(1); // Task was NOT deleted
    }

    [Fact]
    public async Task TasksController_GetTaskById_ReturnsOkWhenFound()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<TasksController>>();
        var taskId = Guid.NewGuid();
        var dto = new DailyTaskDto(taskId, "Test Task", null, false, DateTime.UtcNow);

        mediatorMock.Setup(m => m.Send(It.Is<GetTaskByIdQuery>(q => q.Id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = new TasksController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.GetTaskById(taskId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(dto);
    }

    [Fact]
    public async Task TasksController_UpdateTask_ReturnsOkWhenSuccessful()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<TasksController>>();
        var taskId = Guid.NewGuid();
        var command = new UpdateTaskCommand { Title = "Updated" };
        var dto = new DailyTaskDto(taskId, "Updated", null, false, DateTime.UtcNow);

        mediatorMock.Setup(m => m.Send(It.Is<UpdateTaskCommand>(c => c.Id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var controller = new TasksController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.UpdateTask(taskId, command);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task TasksController_DeleteTask_ReturnsNoContentWhenSuccessful()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<TasksController>>();
        var taskId = Guid.NewGuid();

        mediatorMock.Setup(m => m.Send(It.Is<DeleteTaskCommand>(c => c.Id == taskId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediatR.Unit.Value);

        var controller = new TasksController(mediatorMock.Object, loggerMock.Object);
        var result = await controller.DeleteTask(taskId);

        result.Should().BeOfType<NoContentResult>();
    }
}
