using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Common.Interfaces;
using MOT.Application.DailyGoals.Commands;
using MOT.Application.DailyGoals.Queries;
using MOT.Domain.Entities;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.DailyGoals;

public class DailyGoalHandlerTests
{
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly List<DailyGoal> _dailyGoals;
    private readonly List<StudySession> _sessions;

    public DailyGoalHandlerTests()
    {
        _dailyGoals = new List<DailyGoal>();
        _sessions = new List<StudySession>();
        _dbContextMock = new Mock<IAppDbContext>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        var mockGoalsDbSet = MockDbSetHelper.CreateMockDbSet(_dailyGoals);
        var mockSessionsDbSet = MockDbSetHelper.CreateMockDbSet(_sessions);

        _dbContextMock.Setup(c => c.DailyGoals).Returns(mockGoalsDbSet.Object);
        _dbContextMock.Setup(c => c.StudySessions).Returns(mockSessionsDbSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task GetDailyGoalProgress_WhenNoCustomGoal_UsesDefault60Minutes()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new GetDailyGoalProgressQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetDailyGoalProgressQuery { Date = "2026-10-14" }, CancellationToken.None);

        result.TargetMinutes.Should().Be(60);
        result.IsCustomTarget.Should().BeFalse();
        result.ActualMinutes.Should().Be(0);
        result.RemainingMinutes.Should().Be(60);
        result.ProgressPercentage.Should().Be(0.0);
        result.IsAchieved.Should().BeFalse();
    }

    [Fact]
    public async Task GetDailyGoalProgress_CalculatesActualFromCompletedSessionsOnly()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        // Completed session on 2026-10-14 (45 mins)
        _sessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = DateTime.Parse("2026-10-14T09:00:00Z"),
            EndTime = DateTime.Parse("2026-10-14T09:45:00Z"),
            DurationMinutes = 45,
            IsCompleted = true
        });

        // Incomplete / active session on 2026-10-14 (should be EXCLUDED)
        _sessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = DateTime.Parse("2026-10-14T11:00:00Z"),
            EndTime = null,
            DurationMinutes = 0,
            IsCompleted = false
        });

        // Completed session for another user on 2026-10-14 (should be EXCLUDED)
        _sessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartTime = DateTime.Parse("2026-10-14T14:00:00Z"),
            EndTime = DateTime.Parse("2026-10-14T15:00:00Z"),
            DurationMinutes = 60,
            IsCompleted = true
        });

        // Completed session on another day (should be EXCLUDED)
        _sessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = DateTime.Parse("2026-10-13T10:00:00Z"),
            EndTime = DateTime.Parse("2026-10-13T10:30:00Z"),
            DurationMinutes = 30,
            IsCompleted = true
        });

        var handler = new GetDailyGoalProgressQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetDailyGoalProgressQuery { Date = "2026-10-14" }, CancellationToken.None);

        result.ActualMinutes.Should().Be(45);
        result.RemainingMinutes.Should().Be(15);
        result.ProgressPercentage.Should().Be(75.0);
        result.IsAchieved.Should().BeFalse();
        result.CompletedSessionsCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDailyGoalProgress_WhenTargetMetOrExceeded_SetsAchievedTrueAndCapsProgress()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        _dailyGoals.Add(new DailyGoal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = DateTime.Parse("2026-10-14"),
            TargetMinutes = 60
        });

        // 75 minutes of completed focus
        _sessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = DateTime.Parse("2026-10-14T10:00:00Z"),
            EndTime = DateTime.Parse("2026-10-14T11:15:00Z"),
            DurationMinutes = 75,
            IsCompleted = true
        });

        var handler = new GetDailyGoalProgressQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetDailyGoalProgressQuery { Date = "2026-10-14" }, CancellationToken.None);

        result.ActualMinutes.Should().Be(75);
        result.RemainingMinutes.Should().Be(0);
        result.ProgressPercentage.Should().Be(100.0);
        result.IsAchieved.Should().BeTrue();
        result.IsCustomTarget.Should().BeTrue();
    }

    [Fact]
    public async Task GetDailyGoalProgress_HandlesTimezoneOffsetAccurately()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        // Session at 2026-10-14 20:00 UTC.
        // In UTC+5:30 (+330 minutes), this is 2026-10-15 01:30 AM (local date 2026-10-15).
        _sessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = DateTime.Parse("2026-10-14T20:00:00Z"),
            EndTime = DateTime.Parse("2026-10-14T20:50:00Z"),
            DurationMinutes = 50,
            IsCompleted = true
        });

        var handler = new GetDailyGoalProgressQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // When queried for local date 2026-10-15 with offset +330:
        var resultOct15 = await handler.Handle(new GetDailyGoalProgressQuery
        {
            Date = "2026-10-15",
            TimeZoneOffsetMinutes = 330
        }, CancellationToken.None);

        resultOct15.ActualMinutes.Should().Be(50);

        // When queried for local date 2026-10-14 with offset +330:
        var resultOct14 = await handler.Handle(new GetDailyGoalProgressQuery
        {
            Date = "2026-10-14",
            TimeZoneOffsetMinutes = 330
        }, CancellationToken.None);

        resultOct14.ActualMinutes.Should().Be(0);
    }

    [Fact]
    public async Task SetDailyGoal_WhenNew_InsertsAndReturnsProgress()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new SetDailyGoalCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new SetDailyGoalCommand
        {
            Date = "2026-10-14",
            TargetMinutes = 120
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.TargetMinutes.Should().Be(120);
        result.IsCustomTarget.Should().BeTrue();
        _dailyGoals.Should().HaveCount(1);
        _dailyGoals[0].TargetMinutes.Should().Be(120);
        _dailyGoals[0].UserId.Should().Be(userId);
    }

    [Fact]
    public async Task SetDailyGoal_WhenExisting_UpdatesAndReturnsProgress()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        _dailyGoals.Add(new DailyGoal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = DateTime.Parse("2026-10-14"),
            TargetMinutes = 60
        });

        var handler = new SetDailyGoalCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new SetDailyGoalCommand
        {
            Date = "2026-10-14",
            TargetMinutes = 90
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.TargetMinutes.Should().Be(90);
        _dailyGoals.Should().HaveCount(1);
        _dailyGoals[0].TargetMinutes.Should().Be(90);
        _dailyGoals[0].UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetDailyGoalValidator_ValidatesRangeAndFormat()
    {
        var validator = new SetDailyGoalCommandValidator();

        var lowTarget = validator.Validate(new SetDailyGoalCommand { TargetMinutes = 4 });
        lowTarget.IsValid.Should().BeFalse();

        var highTarget = validator.Validate(new SetDailyGoalCommand { TargetMinutes = 1500 });
        highTarget.IsValid.Should().BeFalse();

        var invalidDate = validator.Validate(new SetDailyGoalCommand { TargetMinutes = 60, Date = "14-10-2026" });
        invalidDate.IsValid.Should().BeFalse();

        var valid = validator.Validate(new SetDailyGoalCommand { TargetMinutes = 60, Date = "2026-10-14" });
        valid.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DailyGoalsController_ReturnsExpectedStatusCodes()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<DailyGoalsController>>();
        var controller = new DailyGoalsController(mediatorMock.Object, loggerMock.Object);

        // GetProgress
        mediatorMock.Setup(m => m.Send(It.IsAny<GetDailyGoalProgressQuery>(), default))
            .ReturnsAsync(new Application.DailyGoals.DTOs.DailyGoalProgressDto { TargetMinutes = 60 });
        var getRes = await controller.GetProgress("2026-10-14", 0);
        getRes.Should().BeOfType<OkObjectResult>();

        // SetDailyGoal
        mediatorMock.Setup(m => m.Send(It.IsAny<SetDailyGoalCommand>(), default))
            .ReturnsAsync(new Application.DailyGoals.DTOs.DailyGoalProgressDto { TargetMinutes = 120 });
        var postRes = await controller.SetDailyGoal(new SetDailyGoalCommand { TargetMinutes = 120 });
        postRes.Should().BeOfType<OkObjectResult>();
    }
}
