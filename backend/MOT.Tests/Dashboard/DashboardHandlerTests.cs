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

    [Fact]
    public async Task GetDashboardSummary_ExcludesIncompleteSessionsFromDailyGoalAndWeeklyActivity()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User { Id = userId, DisplayName = "Test User" });

        var today = DateTime.UtcNow.Date;

        // Incomplete session (running, duration 40 mins so far)
        _studySessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Running Session",
            StartTime = today.AddHours(2),
            DurationMinutes = 40,
            AccumulatedSeconds = 2400,
            IsCompleted = false,
            EndTime = null
        });

        // Completed session (duration 30 mins)
        _studySessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Finished Session",
            StartTime = today.AddHours(5),
            EndTime = today.AddHours(5).AddMinutes(30),
            DurationMinutes = 30,
            AccumulatedSeconds = 1800,
            IsCompleted = true
        });

        var handler = new GetDashboardSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetDashboardSummaryQuery { TimeZoneOffsetMinutes = 0 }, CancellationToken.None);

        // Only the 30-minute completed session should be counted
        result.DailyGoal.ActualMinutes.Should().Be(30);
        result.DailyGoal.CompletedSessionsCount.Should().Be(1);
        result.RecentSessions.Should().HaveCount(1);
        result.RecentSessions[0].Title.Should().Be("Finished Session");
        result.ActiveSession.Should().NotBeNull();
        result.ActiveSession!.Title.Should().Be("Running Session");
    }

    [Fact]
    public async Task GetDashboardSummary_ActiveSession_WhenPaused_ReportsAccumulatedSeconds()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User { Id = userId, DisplayName = "Test User" });

        _studySessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Paused Session",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            DurationMinutes = 15,
            AccumulatedSeconds = 900,
            IsCompleted = false,
            IsPaused = true,
            LastPausedAt = DateTime.UtcNow.AddMinutes(-15)
        });

        var handler = new GetDashboardSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetDashboardSummaryQuery { TimeZoneOffsetMinutes = 0 }, CancellationToken.None);

        result.ActiveSession.Should().NotBeNull();
        result.ActiveSession!.IsPaused.Should().BeTrue();
        result.ActiveSession.AccumulatedSeconds.Should().Be(900);
        result.ActiveSession.ElapsedSeconds.Should().Be(900);
    }

    [Fact]
    public async Task GetDashboardSummary_StreakCalculation_AccountsForMultipleHabitsAndGaps()
    {
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        _users.Add(new User { Id = userId, DisplayName = "Test User" });

        var habitA = new Habit { Id = Guid.NewGuid(), UserId = userId, Title = "Habit A" };
        var habitB = new Habit { Id = Guid.NewGuid(), UserId = userId, Title = "Habit B" };
        _habits.Add(habitA);
        _habits.Add(habitB);

        var today = DateTime.UtcNow.Date;

        // Habit completions forming a historical 3-day streak (5, 4, 3 days ago), then a gap (2 days ago missed),
        // then completed yesterday and today (2-day streak).
        var comps = new List<HabitCompletion>
        {
            new() { Id = Guid.NewGuid(), HabitId = habitA.Id, Habit = habitA, Date = today.AddDays(-5) },
            new() { Id = Guid.NewGuid(), HabitId = habitB.Id, Habit = habitB, Date = today.AddDays(-4) },
            new() { Id = Guid.NewGuid(), HabitId = habitA.Id, Habit = habitA, Date = today.AddDays(-3) },
            // Gap at today - 2
            new() { Id = Guid.NewGuid(), HabitId = habitA.Id, Habit = habitA, Date = today.AddDays(-1) },
            new() { Id = Guid.NewGuid(), HabitId = habitB.Id, Habit = habitB, Date = today }
        };

        habitA.Completions = comps.Where(c => c.HabitId == habitA.Id).ToList();
        habitB.Completions = comps.Where(c => c.HabitId == habitB.Id).ToList();
        _habitCompletions.AddRange(comps);

        var handler = new GetDashboardSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetDashboardSummaryQuery { TimeZoneOffsetMinutes = 0 }, CancellationToken.None);

        result.Streak.CurrentStreak.Should().Be(2);
        result.Streak.IsActiveToday.Should().BeTrue();
        result.Streak.LongestStreak.Should().Be(3);
    }

    [Fact]
    public async Task GetDashboardSummary_StrictTenantIsolation_DoesNotLeakOtherUserData()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        _currentUserServiceMock.Setup(s => s.UserId).Returns(user1.ToString());
        _users.Add(new User { Id = user1, DisplayName = "User One", XP = 100 });
        _users.Add(new User { Id = user2, DisplayName = "User Two", XP = 9999 });

        // User 2's tasks, habits, and sessions
        _tasks.Add(new DailyTask { Id = Guid.NewGuid(), UserId = user2, Title = "User 2 Secret Task" });
        var habit2 = new Habit { Id = Guid.NewGuid(), UserId = user2, Title = "User 2 Habit" };
        _habits.Add(habit2);
        _studySessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user2,
            Title = "User 2 Session",
            DurationMinutes = 120,
            IsCompleted = true,
            EndTime = DateTime.UtcNow
        });

        var handler = new GetDashboardSummaryQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var result = await handler.Handle(new GetDashboardSummaryQuery { TimeZoneOffsetMinutes = 0 }, CancellationToken.None);

        result.Tasks.TotalCount.Should().Be(0);
        result.Habits.TotalCount.Should().Be(0);
        result.RecentSessions.Should().BeEmpty();
        result.Gamification.TotalXp.Should().Be(100);
    }
}
