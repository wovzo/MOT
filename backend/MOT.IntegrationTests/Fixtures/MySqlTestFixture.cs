using System;
using System.Security;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MOT.Infrastructure.Persistence;
using MySqlConnector;
using Xunit;

namespace MOT.IntegrationTests.Fixtures;

public class MySqlTestFixture : IAsyncLifetime
{
    public const string EnvironmentGuardVar = "MOT_ENABLE_MYSQL_INTEGRATION_TESTS";
    public const string ConnectionStringVar = "MOT_MYSQL_TEST_CONNECTION_STRING";

    // Legacy and alias environment variable fallbacks
    public const string AliasConnectionStringVar = "MOT_TEST_CONNECTION_STRING";
    private const string LegacyEnvironmentGuardVar = "MOT_ENABLE_MARIADB_INTEGRATION_TESTS";
    private const string LegacyConnectionStringVar = "MOT_MARIADB_TEST_CONNECTION_STRING";

    private static readonly System.Threading.SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied = false;

    public bool IsAvailable { get; private set; }
    public string? SkipReason { get; private set; }
    public string? ConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        var guardValue = Environment.GetEnvironmentVariable(EnvironmentGuardVar) 
                         ?? Environment.GetEnvironmentVariable(LegacyEnvironmentGuardVar);

        if (!string.Equals(guardValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            IsAvailable = false;
            SkipReason = $"Oracle MySQL integration tests are disabled: '{EnvironmentGuardVar}' is not set to 'true'. " +
                         "To run real Oracle MySQL 8.0 integration tests, configure an isolated local MySQL 8.0.41 instance, " +
                         $"set {EnvironmentGuardVar}=true and {ConnectionStringVar}=\"Server=localhost;Port=3306;Database=mot_test;Uid=...;Pwd=...\".";
            return;
        }

        var connStr = Environment.GetEnvironmentVariable(ConnectionStringVar)
                      ?? Environment.GetEnvironmentVariable(AliasConnectionStringVar)
                      ?? Environment.GetEnvironmentVariable(LegacyConnectionStringVar);

        if (string.IsNullOrWhiteSpace(connStr))
        {
            IsAvailable = false;
            SkipReason = $"Oracle MySQL integration tests failed: '{ConnectionStringVar}' environment variable is missing or empty.";
            return;
        }

        // Safety Guard 1: Validate connection string targets ONLY local host
        var builder = new MySqlConnectionStringBuilder(connStr);
        var server = builder.Server?.ToLowerInvariant() ?? "";
        if (server != "localhost" && server != "127.0.0.1" && server != "::1" && server != "")
        {
            throw new SecurityException(
                $"CRITICAL SAFETY GUARD REJECTION: Integration tests are strictly prohibited from connecting to remote hosts ('{builder.Server}'). Only 'localhost' or '127.0.0.1' is permitted.");
        }

        // Safety Guard 2: Target database name MUST contain 'test' or 'integration'
        var dbName = builder.Database?.ToLowerInvariant() ?? "";
        if (!dbName.Contains("test") && !dbName.Contains("integration"))
        {
            throw new SecurityException(
                $"CRITICAL SAFETY GUARD REJECTION: Database '{builder.Database}' is not unmistakably designated for testing. Database name must contain 'test' or 'integration'.");
        }

        ConnectionString = connStr;

        // Safety Guard 3: Verify connectivity to local test MySQL 8.0 instance
        try
        {
            using var context = CreateDbContext();
            if (!await context.Database.CanConnectAsync())
            {
                IsAvailable = false;
                SkipReason = $"Local Oracle MySQL 8.0 server at '{builder.Server}:{builder.Port}' is unreachable. Verify that local MySQL 8.0 is running and credentials are valid.";
                return;
            }

            // Apply migrations cleanly to disposable test database once across test suites
            await MigrationLock.WaitAsync();
            try
            {
                if (!_migrationsApplied)
                {
                    await context.Database.MigrateAsync();
                    _migrationsApplied = true;
                }
            }
            finally
            {
                MigrationLock.Release();
            }

            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            SkipReason = $"Failed to initialize local Oracle MySQL 8.0 test database: {ex.Message}";
        }
    }

    public void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(
                $"Oracle MySQL 8.0 integration test cannot execute because the target test database is unavailable or not opted-in. " +
                $"Reason: {SkipReason}");
        }
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    public AppDbContext CreateDbContext()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException("Integration test connection string is not initialized.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 41)))
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContext(options);
    }

    public async Task ResetDataAsync()
    {
        EnsureAvailable();

        using var context = CreateDbContext();
        // Truncate/delete test data in reverse foreign key order
        await context.Database.ExecuteSqlRawAsync("DELETE FROM UserAchievements;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM XpTransactions;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM HabitCompletions;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM StudySessions;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Tasks;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM DailyGoals;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Subjects;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Habits;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM RoomParticipants;");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Users;");
    }
}
