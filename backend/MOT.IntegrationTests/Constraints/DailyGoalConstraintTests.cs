using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;
using MOT.IntegrationTests.Fixtures;
using Xunit;

namespace MOT.IntegrationTests.Constraints;

[Trait("Category", "DatabaseIntegration")]
public class DailyGoalConstraintTests : IClassFixture<MySqlTestFixture>
{
    private readonly MySqlTestFixture _fixture;

    public DailyGoalConstraintTests(MySqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DailyGoal_DuplicateDateForSameUser_ThrowsDbUpdateException()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"goal_{Guid.NewGuid():N}@test.com", DisplayName = "Goal Tester" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        var goal1 = new DailyGoal { Id = Guid.NewGuid(), UserId = user.Id, Date = today, TargetMinutes = 60 };
        context.DailyGoals.Add(goal1);
        await context.SaveChangesAsync();

        var goal2 = new DailyGoal { Id = Guid.NewGuid(), UserId = user.Id, Date = today, TargetMinutes = 90 };
        context.DailyGoals.Add(goal2);

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("Oracle MySQL unique index on {UserId, Date} must reject duplicate daily goals for the same day");
    }

    [Fact]
    public async Task DailyGoal_WhenUserDeleted_CascadesDeleteToGoals()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"goalcasc_{Guid.NewGuid():N}@test.com", DisplayName = "Cascade Tester" };
        context.Users.Add(user);

        var goal = new DailyGoal { Id = Guid.NewGuid(), UserId = user.Id, Date = DateTime.UtcNow.Date, TargetMinutes = 45 };
        context.DailyGoals.Add(goal);
        await context.SaveChangesAsync();

        // Delete User
        context.Users.Remove(user);
        await context.SaveChangesAsync();

        // Verify goal is deleted via cascade
        using var verifyContext = _fixture.CreateDbContext();
        var reloadedGoal = await verifyContext.DailyGoals.FindAsync(goal.Id);
        reloadedGoal.Should().BeNull("Deleting user must cascade delete linked daily goals in MySQL");
    }
}
