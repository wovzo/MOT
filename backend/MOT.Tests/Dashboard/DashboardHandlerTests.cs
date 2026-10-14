using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.DailyGoals.DTOs;
using MOT.Application.Dashboard.DTOs;
using MOT.Application.Dashboard.Queries;
using MOT.Application.Gamification.DTOs;
using MOT.Domain.Entities;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Dashboard;

public class DashboardHandlerTests
{
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly List<User> _users;
    private readonly List<UserAchievement> _achievements;
    private readonly List<DailyGoal> _dailyGoals;
    private readonly List<StudySession> _studySessions;
    private readonly List<DailyTask> _tasks;
    private readonly List<Habit> _habits;
    private readonly List<HabitCompletion> _habitCompletions;
    private readonly List<Subject> _subjects;

    public DashboardHandlerTests()
    {
        _users = new List<User>();
        _achievements = new List<UserAchievement>();
        _dailyGoals = new List<DailyGoal>();
        _studySessions = new List<StudySession>();
        _tasks = new List<DailyTask>();
        _habits = new List<Habit>();
        _habitCompletions = new List<HabitCompletion>();
        _subjects = new List<Subject>();

        _dbContextMock = new Mock<IAppDbContext>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        _dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(_users).Object);
        _dbContextMock.Setup(c => c.UserAchievements).Returns(MockDbSetHelper.CreateMockDbSet(_achievements).Object);
        _dbContextMock.Setup(c => c.DailyGoals).Returns(MockDbSetHelper.CreateMockDbSet(_dailyGoals).Object);
        _dbContextMock.Setup(c => c.StudySessions).Returns(MockDbSetHelper.CreateMockDbSet(_studySessions).Object);
        _dbContextMock.Setup(c => c.Tasks).Returns(MockDbSetHelper.CreateMockDbSet(_tasks).Object);
        _dbContextMock.Setup(c => c.Habits).Returns(MockDbSetHelper.CreateMockDbSet(_habits).Object);
        _dbContextMock.Setup(c => c.HabitCompletions).Returns(MockDbSetHelper.CreateMockDbSet(_habitCompletions).Object);
        _dbContextMock.Setup(c => c.Subjects).Returns(MockDbSetHelper.CreateMockDbSet(_subjects).Object);
    }

    [Fact]
    public async Task GetDashboardSummary_WhenUnauthenticated_ThrowsUnauthorized()
    {
        _currentUserServiceMock.Setup(s => s.UserId).Returns((string?)null);
        var handler = new GetDashboardSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        var act = () => handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetDashboardSummary_WhenUserNotFound_ThrowsUserNotFound()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var handler = new GetDashboardSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        var act = () => handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);
        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task GetDashboardSummary_WhenUserHasEmptyData_ReturnsHealthyDefaults()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User
        {
            Id = userId,
            Email = "scholar@example.com",
            DisplayName = "Scholar Student",
            XP = 0
        });

        var handler = new GetDashboardSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetDashboardSummaryQuery { TimeZoneOffsetMinutes = 0 }, CancellationToken.None);

        result.Should().NotBeNull();
        result.DailyGoal.TargetMinutes.Should().Be(60);
        result.DailyGoal.ActualMinutes.Should().Be(0);
        result.DailyGoal.RemainingMinutes.Should().Be(60);
        result.DailyGoal.ProgressPercentage.Should().Be(0.0);
        result.DailyGoal.IsAchieved.Should().BeFalse();
        result.ActiveSession.Should().BeNull();
        result.Tasks.TotalCount.Should().Be(0);
        result.Tasks.PendingCount.Should().Be(0);
        result.Habits.TotalCount.Should().Be(0);
        result.Streak.CurrentStreak.Should().Be(0);
        result.Gamification.Level.Should().Be(1);
        result.Gamification.TotalXp.Should().Be(0);
        result.Subjects.Should().BeEmpty();
        result.RecentSessions.Should().BeEmpty();
        result.WeeklyActivity.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetDashboardSummary_WithPopulatedData_ReturnsAccurateMetrics()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var user = new User
        {
            Id = userId,
            Email = "student@example.com",
            DisplayName = "Active Student",
            XP = 250
        };
        _users.Add(user);

        // Subject
        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Mathematics",
            ColorHex = "#4A90E2"
        };
        _subjects.Add(subject);

        // Daily Goal
        var today = DateTime.UtcNow.Date;
        _dailyGoals.Add(new DailyGoal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = today,
            TargetMinutes = 90
        });

        // Completed Study Session Today
        _studySessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = subject.Id,
            Subject = subject,
            Title = "Calculus Practice",
            StartTime = today.AddHours(9),
            EndTime = today.AddHours(9).AddMinutes(45),
            DurationMinutes = 45,
            AccumulatedSeconds = 2700,
            IsCompleted = true
        });

        // Active Study Session
        _studySessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = subject.Id,
            Subject = subject,
            Title = "Linear Algebra",
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            AccumulatedSeconds = 1200,
            IsCompleted = false,
            IsPaused = false
        });

        // Tasks
        _tasks.Add(new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Read chapter 4",
            IsCompleted = true,
            CreatedAt = DateTime.UtcNow.AddHours(-3)
        });
        _tasks.Add(new DailyTask
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Complete problem set",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });

        // Habits and Streak
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Morning Review",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        var compYesterday = new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Habit = habit, Date = today.AddDays(-1) };
        var compToday = new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Habit = habit, Date = today };
        habit.Completions = new List<HabitCompletion> { compYesterday, compToday };
        _habits.Add(habit);
        _habitCompletions.Add(compYesterday);
        _habitCompletions.Add(compToday);

        var handler = new GetDashboardSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetDashboardSummaryQuery { TimeZoneOffsetMinutes = 0 }, CancellationToken.None);

        result.Should().NotBeNull();
        // Daily goal check
        result.DailyGoal.TargetMinutes.Should().Be(90);
        result.DailyGoal.ActualMinutes.Should().Be(45);
        result.DailyGoal.RemainingMinutes.Should().Be(45);
        result.DailyGoal.ProgressPercentage.Should().Be(50.0);
        result.DailyGoal.IsAchieved.Should().BeFalse();

        // Active session check
        result.ActiveSession.Should().NotBeNull();
        result.ActiveSession!.Title.Should().Be("Linear Algebra");
        result.ActiveSession.SubjectName.Should().Be("Mathematics");
        result.ActiveSession.IsCompleted.Should().BeFalse();

        // Tasks check
        result.Tasks.TotalCount.Should().Be(2);
        result.Tasks.CompletedCount.Should().Be(1);
        result.Tasks.PendingCount.Should().Be(1);
        result.Tasks.CompletionPercentage.Should().Be(50.0);
        result.Tasks.TopPendingTasks.Should().HaveCount(1);
        result.Tasks.TopPendingTasks[0].Title.Should().Be("Complete problem set");

        // Habits and streak check
        result.Habits.TotalCount.Should().Be(1);
        result.Habits.CompletedTodayCount.Should().Be(1);
        result.Streak.CurrentStreak.Should().Be(2);
        result.Streak.IsActiveToday.Should().BeTrue();

        // Gamification check
        result.Gamification.TotalXp.Should().Be(250);
        result.Gamification.Level.Should().BeGreaterThan(1);

        // Subjects & Recent sessions
        result.Subjects.Should().HaveCount(1);
        result.RecentSessions.Should().HaveCount(1);
        result.RecentSessions[0].Title.Should().Be("Calculus Practice");
        result.WeeklyActivity.Should().HaveCount(7);
    }

    [Fact]
    public async Task DashboardController_GetDashboardSummary_ReturnsOkResult()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<DashboardController>>();
        var fakeSummary = new DashboardSummaryDto
        {
            DailyGoal = new DailyGoalProgressDto { TargetMinutes = 60, ActualMinutes = 30 },
            Streak = new DashboardStreakSummaryDto { CurrentStreak = 3, IsActiveToday = true },
            Tasks = new DashboardTaskSummaryDto { TotalCount = 4, CompletedCount = 2 },
            Habits = new DashboardHabitSummaryDto { TotalCount = 2, CompletedTodayCount = 1 },
            Gamification = new GamificationSummaryDto(2, 200, 100, 300, 100, 200, 0.5, 50.0, 8, 2)
        };

        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetDashboardSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeSummary);

        var controller = new DashboardController(mediatorMock.Object, loggerMock.Object);
        var actionResult = await controller.GetDashboardSummary(0);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(fakeSummary);
    }
}
