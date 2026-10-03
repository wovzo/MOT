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
using MOT.Application.DTOs;
using MOT.Application.Habits.Commands;
using MOT.Application.Habits.Queries;
using MOT.Domain.Entities;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Habits;

public class HabitHandlerTests
{
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly List<Habit> _habits;
    private readonly List<HabitCompletion> _completions;

    public HabitHandlerTests()
    {
        _habits = new List<Habit>();
        _completions = new List<HabitCompletion>();
        _dbContextMock = new Mock<IAppDbContext>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        var mockHabitsDbSet = MockDbSetHelper.CreateMockDbSet(_habits);
        var mockCompletionsDbSet = MockDbSetHelper.CreateMockDbSet(_completions);

        _dbContextMock.Setup(c => c.Habits).Returns(mockHabitsDbSet.Object);
        _dbContextMock.Setup(c => c.HabitCompletions).Returns(mockCompletionsDbSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    // ==========================================
    // 1. Creation Tests
    // ==========================================

    [Fact]
    public async Task CreateHabit_WhenAuthenticated_CreatesHabitForUserWithGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new CreateHabitCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new CreateHabitCommand
        {
            Title = "Morning Meditation",
            Description = "10 minutes of mindfulness"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        _habits.Should().HaveCount(1);
        var createdHabit = _habits.Single();
        createdHabit.Id.Should().Be(result);
        createdHabit.UserId.Should().Be(userId);
        createdHabit.Title.Should().Be("Morning Meditation");
        createdHabit.Description.Should().Be("10 minutes of mindfulness");

        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-guid")]
    public async Task CreateHabit_WhenUserIdIsMissingOrInvalid_ThrowsUnauthorizedAccessException(string? invalidUserId)
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);

        var handler = new CreateHabitCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new CreateHabitCommand { Title = "Exercise", Description = "Daily gym" };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _habits.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Drink Water")]
    [InlineData("Reading Books")]
    public void CreateHabitValidator_WithValidTitle_PassesValidation(string title)
    {
        var validator = new CreateHabitCommandValidator();
        var command = new CreateHabitCommand { Title = title, Description = "Some description" };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateHabitValidator_WithEmptyOrWhitespaceTitle_FailsValidation(string? emptyTitle)
    {
        var validator = new CreateHabitCommandValidator();
        var command = new CreateHabitCommand { Title = emptyTitle!, Description = "Desc" };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void CreateHabitValidator_WithTooLongTitle_FailsValidation()
    {
        var validator = new CreateHabitCommandValidator();
        var command = new CreateHabitCommand { Title = new string('X', 101), Description = "Desc" };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    // ==========================================
    // 2. Retrieval & Scoping Tests
    // ==========================================

    [Fact]
    public async Task GetHabits_ReturnsOnlyAuthenticatedUsersHabits()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userA.ToString());

        var habitA1 = new Habit { Id = Guid.NewGuid(), UserId = userA, Title = "User A - Habit 1", CreatedAt = DateTime.UtcNow.AddDays(-5) };
        var habitA2 = new Habit { Id = Guid.NewGuid(), UserId = userA, Title = "User A - Habit 2", CreatedAt = DateTime.UtcNow.AddDays(-2) };
        var habitB = new Habit { Id = Guid.NewGuid(), UserId = userB, Title = "User B - Habit 1", CreatedAt = DateTime.UtcNow.AddDays(-3) };

        _habits.AddRange(new[] { habitA1, habitA2, habitB });

        var handler = new GetHabitsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetHabitsQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Select(h => h.Id).Should().Contain(new[] { habitA1.Id, habitA2.Id });
        result.Select(h => h.Id).Should().NotContain(habitB.Id);
    }

    [Fact]
    public void ClientInput_CannotOverrideAuthenticatedUserId()
    {
        // Assert contract: Commands and queries must not expose client-controllable UserId properties
        typeof(CreateHabitCommand).GetProperty("UserId").Should().BeNull(
            "CreateHabitCommand must not allow client to submit a UserId property");
        typeof(ToggleHabitCompletionCommand).GetProperty("UserId").Should().BeNull(
            "ToggleHabitCompletionCommand must not allow client to submit a UserId property");
        typeof(GetHabitsQuery).GetProperty("UserId").Should().BeNull(
            "GetHabitsQuery must not allow client to submit a UserId property");
    }

    // ==========================================
    // 3. Toggle & Ownership Tests
    // ==========================================

    [Fact]
    public async Task ToggleHabit_WhenNotCompletedToday_AddsCompletionAndReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var habitId = Guid.NewGuid();

        var habit = new Habit { Id = habitId, UserId = userId, Title = "Hydration" };
        _habits.Add(habit);

        var handler = new ToggleHabitCompletionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new ToggleHabitCompletionCommand { HabitId = habitId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _completions.Should().HaveCount(1);
        _completions.Single().HabitId.Should().Be(habitId);
        _completions.Single().Date.Date.Should().Be(DateTime.UtcNow.Date);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleHabit_WhenAlreadyCompletedToday_RemovesCompletionAndReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var habitId = Guid.NewGuid();

        var habit = new Habit { Id = habitId, UserId = userId, Title = "Hydration" };
        var completion = new HabitCompletion { Id = Guid.NewGuid(), HabitId = habitId, Date = DateTime.UtcNow.Date };

        _habits.Add(habit);
        _completions.Add(completion);

        var handler = new ToggleHabitCompletionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new ToggleHabitCompletionCommand { HabitId = habitId };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _completions.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleHabit_WhenHabitNotFound_ThrowsHabitNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var nonExistentHabitId = Guid.NewGuid();

        var handler = new ToggleHabitCompletionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new ToggleHabitCompletionCommand { HabitId = nonExistentHabitId };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<HabitNotFoundException>();
        ex.WithMessage($"*{nonExistentHabitId}*");
        _completions.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleHabit_WhenHabitBelongsToOtherUser_ThrowsHabitNotFoundExceptionAndDoesNotMutate()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userA.ToString());
        var habitBId = Guid.NewGuid();

        var habitB = new Habit { Id = habitBId, UserId = userB, Title = "User B Private Habit" };
        _habits.Add(habitB);

        var handler = new ToggleHabitCompletionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new ToggleHabitCompletionCommand { HabitId = habitBId };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<HabitNotFoundException>();
        ex.WithMessage($"*{habitBId}*");
        _completions.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ==========================================
    // 4. Streak Calculation Behavior Tests
    // ==========================================

    [Fact]
    public async Task GetHabits_CalculatesStreakCorrectly_WhenCompletedConsecutiveDaysEndingToday()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var today = DateTime.UtcNow.Date;

        var habit = new Habit { Id = Guid.NewGuid(), UserId = userId, Title = "Guitar Practice" };
        habit.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today });
        habit.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-1) });
        habit.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-2) });

        _habits.Add(habit);

        var handler = new GetHabitsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetHabitsQuery(), CancellationToken.None);

        // Assert
        var dto = result.Single();
        dto.IsCompletedToday.Should().BeTrue();
        dto.CurrentStreak.Should().Be(3);
    }

    [Fact]
    public async Task GetHabits_CalculatesStreakCorrectly_WhenCompletedConsecutiveDaysEndingYesterday()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var today = DateTime.UtcNow.Date;

        // Not completed today, but completed yesterday and day before
        var habit = new Habit { Id = Guid.NewGuid(), UserId = userId, Title = "Workout" };
        habit.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-1) });
        habit.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-2) });

        _habits.Add(habit);

        var handler = new GetHabitsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetHabitsQuery(), CancellationToken.None);

        // Assert
        var dto = result.Single();
        dto.IsCompletedToday.Should().BeFalse();
        dto.CurrentStreak.Should().Be(2); // Streak preserved from yesterday per current algorithm
    }

    [Fact]
    public async Task GetHabits_CalculatesZeroStreak_WhenStreakBrokenTwoDaysAgo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var today = DateTime.UtcNow.Date;

        // Completed 3 days ago, but missed yesterday and today
        var habit = new Habit { Id = Guid.NewGuid(), UserId = userId, Title = "Stretching" };
        habit.Completions.Add(new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today.AddDays(-3) });

        _habits.Add(habit);

        var handler = new GetHabitsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetHabitsQuery(), CancellationToken.None);

        // Assert
        var dto = result.Single();
        dto.IsCompletedToday.Should().BeFalse();
        dto.CurrentStreak.Should().Be(0);
    }

    [Fact]
    public async Task GetHabits_CalculatesZeroStreak_WhenNoCompletionsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var habit = new Habit { Id = Guid.NewGuid(), UserId = userId, Title = "Floss" };
        _habits.Add(habit);

        var handler = new GetHabitsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetHabitsQuery(), CancellationToken.None);

        // Assert
        var dto = result.Single();
        dto.IsCompletedToday.Should().BeFalse();
        dto.CurrentStreak.Should().Be(0);
    }

    // ==========================================
    // 5. Controller Semantics Tests
    // ==========================================

    [Fact]
    public async Task HabitsController_GetHabits_ReturnsOkResult()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<HabitsController>>();
        var expectedDtos = new List<HabitDto>
        {
            new HabitDto { Id = Guid.NewGuid(), Title = "Reading", CurrentStreak = 5, IsCompletedToday = true }
        };
        mediatorMock.Setup(m => m.Send(It.IsAny<GetHabitsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDtos);

        var controller = new HabitsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetHabits();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedDtos);
    }

    [Fact]
    public async Task HabitsController_CreateHabit_ReturnsOkResultWithId()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<HabitsController>>();
        var createdId = Guid.NewGuid();
        mediatorMock.Setup(m => m.Send(It.IsAny<CreateHabitCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdId);

        var controller = new HabitsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.CreateHabit(new CreateHabitCommand { Title = "Code Daily", Description = "1 commit" });

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { Id = createdId });
    }

    [Fact]
    public async Task HabitsController_CreateHabit_WhenValidationFails_ReturnsBadRequestResult()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<HabitsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<CreateHabitCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FluentValidation.ValidationException(new[] { new ValidationFailure("Title", "Title is required.") }));

        var controller = new HabitsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.CreateHabit(new CreateHabitCommand { Title = "" });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HabitsController_ToggleHabit_ReturnsOkResultWithIsCompletedToday()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<HabitsController>>();
        var habitId = Guid.NewGuid();
        mediatorMock.Setup(m => m.Send(It.IsAny<ToggleHabitCompletionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new HabitsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.ToggleHabit(habitId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { IsCompletedToday = true });
    }

    [Fact]
    public async Task HabitsController_ToggleHabit_WhenNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<HabitsController>>();
        var habitId = Guid.NewGuid();
        mediatorMock.Setup(m => m.Send(It.IsAny<ToggleHabitCompletionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HabitNotFoundException(habitId));

        var controller = new HabitsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.ToggleHabit(habitId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task HabitsController_WhenUnexpectedExceptionOccurs_ReturnsStatusCode500()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<HabitsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<GetHabitsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        var controller = new HabitsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetHabits();

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }
}
