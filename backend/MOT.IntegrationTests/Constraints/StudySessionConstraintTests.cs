using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;
using MOT.IntegrationTests.Fixtures;
using Xunit;

namespace MOT.IntegrationTests.Constraints;

[Trait("Category", "DatabaseIntegration")]
public class StudySessionConstraintTests : IClassFixture<MySqlTestFixture>
{
    private readonly MySqlTestFixture _fixture;

    public StudySessionConstraintTests(MySqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StudySession_ActiveUserIdUniqueness_PreventsTwoActiveSessionsForSameUser()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"session_{Guid.NewGuid():N}@test.com", DisplayName = "Session Tester" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var session1 = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ActiveUserId = user.Id,
            Title = "First Active Session",
            StartTime = DateTime.UtcNow,
            IsCompleted = false
        };
        context.StudySessions.Add(session1);
        await context.SaveChangesAsync();

        // Attempting to start a second active session with the same ActiveUserId
        var session2 = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ActiveUserId = user.Id,
            Title = "Second Active Session",
            StartTime = DateTime.UtcNow,
            IsCompleted = false
        };
        context.StudySessions.Add(session2);

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("Oracle MySQL unique index on ActiveUserId must reject two concurrent active sessions for the same user");
    }

    [Fact]
    public async Task StudySession_CompletedSessions_AllowMultipleNullActiveUserId()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"multisess_{Guid.NewGuid():N}@test.com", DisplayName = "Multi Session Tester" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var session1 = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ActiveUserId = null,
            Title = "Completed Session 1",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            IsCompleted = true,
            DurationMinutes = 60
        };
        var session2 = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ActiveUserId = null,
            Title = "Completed Session 2",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow,
            IsCompleted = true,
            DurationMinutes = 60
        };

        context.StudySessions.AddRange(session1, session2);
        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().NotThrowAsync("Null ActiveUserId values must not collide under unique index in MySQL");
    }

    [Fact]
    public async Task StudySession_WhenUserDeleted_CascadesDeleteToSessions()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"sesscasc_{Guid.NewGuid():N}@test.com", DisplayName = "Cascade Tester" };
        context.Users.Add(user);

        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Test Session",
            StartTime = DateTime.UtcNow,
            IsCompleted = false
        };
        context.StudySessions.Add(session);
        await context.SaveChangesAsync();

        // Delete User
        context.Users.Remove(user);
        await context.SaveChangesAsync();

        using var verifyContext = _fixture.CreateDbContext();
        var reloadedSession = await verifyContext.StudySessions.FindAsync(session.Id);
        reloadedSession.Should().BeNull("Deleting user must cascade delete linked study sessions in MySQL");
    }
}
