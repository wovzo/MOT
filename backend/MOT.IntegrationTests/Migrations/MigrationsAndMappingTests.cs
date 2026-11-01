using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MOT.IntegrationTests.Fixtures;
using Xunit;

namespace MOT.IntegrationTests.Migrations;

[Trait("Category", "DatabaseIntegration")]
public class MigrationsAndMappingTests : IClassFixture<MySqlTestFixture>
{
    private readonly MySqlTestFixture _fixture;

    public MigrationsAndMappingTests(MySqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migrations_WhenAppliedToMySql_SucceedsWithoutPendingMigrations()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();

        // Verify that migrations apply cleanly to Oracle MySQL 8.0.41
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        pendingMigrations.Should().BeEmpty("all migrations must be successfully applied to the MySQL test database");

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().NotBeEmpty("applied migrations table must contain historical migrations");
    }

    [Fact]
    public async Task SchemaMapping_AllCoreEntities_CanQueryEmptyTablesWithoutErrors()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();

        // Verify Oracle MySQL 8.0 SQL generation and table mappings for all 10 domain entities
        var usersCount = await context.Users.CountAsync();
        var subjectsCount = await context.Subjects.CountAsync();
        var dailyGoalsCount = await context.DailyGoals.CountAsync();
        var tasksCount = await context.Tasks.CountAsync();
        var habitsCount = await context.Habits.CountAsync();
        var completionsCount = await context.HabitCompletions.CountAsync();
        var sessionsCount = await context.StudySessions.CountAsync();
        var xpCount = await context.XpTransactions.CountAsync();
        var achievementsCount = await context.UserAchievements.CountAsync();
        var participantsCount = await context.RoomParticipants.CountAsync();

        usersCount.Should().BeGreaterThanOrEqualTo(0);
        subjectsCount.Should().BeGreaterThanOrEqualTo(0);
        dailyGoalsCount.Should().BeGreaterThanOrEqualTo(0);
        tasksCount.Should().BeGreaterThanOrEqualTo(0);
        habitsCount.Should().BeGreaterThanOrEqualTo(0);
        completionsCount.Should().BeGreaterThanOrEqualTo(0);
        sessionsCount.Should().BeGreaterThanOrEqualTo(0);
        xpCount.Should().BeGreaterThanOrEqualTo(0);
        achievementsCount.Should().BeGreaterThanOrEqualTo(0);
        participantsCount.Should().BeGreaterThanOrEqualTo(0);
    }
}
