using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;
using MOT.IntegrationTests.Fixtures;
using Xunit;

namespace MOT.IntegrationTests.Constraints;

[Trait("Category", "DatabaseIntegration")]
public class HabitCompletionConstraintTests : IClassFixture<MySqlTestFixture>
{
    private readonly MySqlTestFixture _fixture;

    public HabitCompletionConstraintTests(MySqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HabitCompletion_DuplicateDateForSameHabit_ThrowsDbUpdateException()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"habit_{Guid.NewGuid():N}@test.com", DisplayName = "Habit Tester" };
        context.Users.Add(user);

        var habit = new Habit { Id = Guid.NewGuid(), UserId = user.Id, Title = "Read 30 mins" };
        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        var completion1 = new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today };
        context.HabitCompletions.Add(completion1);
        await context.SaveChangesAsync();

        var completion2 = new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = today };
        context.HabitCompletions.Add(completion2);

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("Oracle MySQL unique constraint on {HabitId, Date} must prevent double completion on same calendar day");
    }

    [Fact]
    public async Task HabitCompletion_WhenHabitDeleted_CascadesDeleteToCompletions()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"habitcasc_{Guid.NewGuid():N}@test.com", DisplayName = "Cascade Tester" };
        context.Users.Add(user);

        var habit = new Habit { Id = Guid.NewGuid(), UserId = user.Id, Title = "Daily Exercise" };
        context.Habits.Add(habit);

        var completion = new HabitCompletion { Id = Guid.NewGuid(), HabitId = habit.Id, Date = DateTime.UtcNow.Date };
        context.HabitCompletions.Add(completion);
        await context.SaveChangesAsync();

        // Delete Habit
        context.Habits.Remove(habit);
        await context.SaveChangesAsync();

        using var verifyContext = _fixture.CreateDbContext();
        var reloadedCompletion = await verifyContext.HabitCompletions.FindAsync(completion.Id);
        reloadedCompletion.Should().BeNull("Deleting habit must cascade delete its completion records in MySQL");
    }
}
