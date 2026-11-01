using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Dashboard.Queries;
using MOT.Domain.Entities;
using MOT.IntegrationTests.Fixtures;
using Xunit;

namespace MOT.IntegrationTests.Dashboard;

[Trait("Category", "DatabaseIntegration")]
public class DashboardAggregateQueryTests : IClassFixture<MySqlTestFixture>
{
    private readonly MySqlTestFixture _fixture;

    public DashboardAggregateQueryTests(MySqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DashboardAggregateQuery_WhenExecutedAgainstMySql_ComputesAccurateMetricsAndDateBoundaries()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"dash_{Guid.NewGuid():N}@test.com",
            DisplayName = "Dashboard User",
            XP = 250,
            Level = 3
        };
        context.Users.Add(user);

        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Organic Chemistry",
            ColorHex = "#4CAF50"
        };
        context.Subjects.Add(subject);

        var today = DateTime.UtcNow.Date;
        var dailyGoal = new DailyGoal
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Date = today,
            TargetMinutes = 120
        };
        context.DailyGoals.Add(dailyGoal);

        // Completed session today (60 mins, no pause)
        var completedSession = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SubjectId = subject.Id,
            Title = "Morning Chemistry",
            StartTime = DateTime.UtcNow.Date.AddHours(9),
            EndTime = DateTime.UtcNow.Date.AddHours(10),
            IsCompleted = true,
            DurationMinutes = 60,
            AccumulatedSeconds = 3600
        };
        context.StudySessions.Add(completedSession);

        // Active session right now
        var activeSession = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ActiveUserId = user.Id,
            SubjectId = subject.Id,
            Title = "Afternoon Study",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            IsCompleted = false,
            IsPaused = false,
            AccumulatedSeconds = 1800
        };
        context.StudySessions.Add(activeSession);

        // Habit completed today
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Daily Vocab"
        };
        context.Habits.Add(habit);
        context.HabitCompletions.Add(new HabitCompletion
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            Date = today
        });

        await context.SaveChangesAsync();

        // Query directly using handler with AppDbContext
        var mockUserService = new Moq.Mock<MOT.Application.Common.Interfaces.ICurrentUserService>();
        mockUserService.Setup(s => s.UserId).Returns(user.Id.ToString());

        var handler = new GetDashboardSummaryQueryHandler(context, mockUserService.Object);
        var query = new GetDashboardSummaryQuery { TimeZoneOffsetMinutes = 0 };

        var summary = await handler.Handle(query, CancellationToken.None);

        summary.Should().NotBeNull();
        summary.DailyGoal.TargetMinutes.Should().Be(120);
        summary.DailyGoal.ActualMinutes.Should().Be(60, "only completed sessions count toward today focus minutes");
        summary.DailyGoal.RemainingMinutes.Should().Be(60);
        summary.DailyGoal.IsAchieved.Should().BeFalse();

        summary.ActiveSession.Should().NotBeNull();
        summary.ActiveSession!.Title.Should().Be("Afternoon Study");
        summary.ActiveSession.SubjectName.Should().Be("Organic Chemistry");

        summary.Streak.CurrentStreak.Should().BeGreaterThanOrEqualTo(1);
        summary.Gamification.TotalXp.Should().Be(250);
        summary.Gamification.Level.Should().Be(3);
    }
}
