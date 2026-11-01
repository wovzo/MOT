using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;
using MOT.IntegrationTests.Fixtures;
using Xunit;

namespace MOT.IntegrationTests.Gamification;

[Trait("Category", "DatabaseIntegration")]
public class GamificationConcurrencyAndXpTests : IClassFixture<MySqlTestFixture>
{
    private readonly MySqlTestFixture _fixture;

    public GamificationConcurrencyAndXpTests(MySqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task XpTransaction_DuplicateSourceIdForSameUserAndType_ThrowsDbUpdateException()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"xp_{Guid.NewGuid():N}@test.com", DisplayName = "XP Tester" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tx1 = new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceType = "Task",
            SourceId = "task-123",
            Amount = 15,
            Description = "Completed task",
            CreatedAt = DateTime.UtcNow
        };
        context.XpTransactions.Add(tx1);
        await context.SaveChangesAsync();

        var tx2 = new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceType = "Task",
            SourceId = "task-123",
            Amount = 15,
            Description = "Completed task duplicate",
            CreatedAt = DateTime.UtcNow
        };
        context.XpTransactions.Add(tx2);

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("Oracle MySQL unique index IX_XpTransactions_UserId_SourceType_SourceId must reject duplicate XP awards");
    }

    [Fact]
    public async Task MultiInstance_ConcurrentXpUpdatesWithoutDistributedLock_DemonstratesLostUpdateRisk()
    {
        _fixture.EnsureAvailable();

        // This test simulates two separate API server instances (Instance A and Instance B)
        // processing two distinct XP award events (e.g. Task A completed and Task B completed)
        // for the same user concurrently without sharing an in-process UserConcurrencyLock.
        using var setupContext = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"race_{Guid.NewGuid():N}@test.com", DisplayName = "Race Tester", XP = 100, Level = 2 };
        setupContext.Users.Add(user);
        await setupContext.SaveChangesAsync();

        // Instance A context
        using var contextA = _fixture.CreateDbContext();
        var userA = await contextA.Users.FindAsync(user.Id);

        // Instance B context
        using var contextB = _fixture.CreateDbContext();
        var userB = await contextB.Users.FindAsync(user.Id);

        // Instance A awards 20 XP for Task 1
        userA!.XP += 20;
        contextA.XpTransactions.Add(new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceType = "Task",
            SourceId = "task-A",
            Amount = 20,
            Description = "Task A",
            CreatedAt = DateTime.UtcNow
        });

        // Instance B awards 30 XP for Task 2 (still based on stale read of 100 XP)
        userB!.XP += 30;
        contextB.XpTransactions.Add(new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SourceType = "Task",
            SourceId = "task-B",
            Amount = 30,
            Description = "Task B",
            CreatedAt = DateTime.UtcNow
        });

        // Instance A commits: user.XP becomes 120
        await contextA.SaveChangesAsync();

        // Instance B commits: user.XP becomes 130, overwriting Instance A's increment!
        await contextB.SaveChangesAsync();

        // Reload user from database
        using var verifyContext = _fixture.CreateDbContext();
        var reloadedUser = await verifyContext.Users.FindAsync(user.Id);
        var transactions = await verifyContext.XpTransactions.Where(t => t.UserId == user.Id).ToListAsync();

        transactions.Should().HaveCount(2, "both transaction rows were successfully inserted because SourceIds differed");
        
        // EVIDENCE: Without an optimistic concurrency token or distributed lock, user.XP is 130 instead of 150!
        // This concrete proof validates why MOT deployment MUST remain single-instance until
        // distributed locking (e.g. Redis) or atomic database-level increments are introduced.
        reloadedUser!.XP.Should().Be(130, "last write wins without distributed concurrency control");
    }
}
