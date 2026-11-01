using System;
using System.Security;
using System.Threading.Tasks;
using FluentAssertions;
using MOT.IntegrationTests.Fixtures;
using Xunit;

namespace MOT.IntegrationTests.Safety;

[Trait("Category", "SafetyGuard")]
public class SafetyGuardTests
{
    [Fact]
    public async Task SafetyGuard_WhenGuardEnvironmentVariableMissing_ReportsUnavailable()
    {
        Environment.SetEnvironmentVariable(MySqlTestFixture.EnvironmentGuardVar, null);
        Environment.SetEnvironmentVariable(MySqlTestFixture.ConnectionStringVar, null);

        var fixture = new MySqlTestFixture();
        await fixture.InitializeAsync();

        fixture.IsAvailable.Should().BeFalse();
        fixture.SkipReason.Should().Contain("MOT_ENABLE_MYSQL_INTEGRATION_TESTS");
    }

    [Theory]
    [InlineData("Server=mysql.gb.stackcp.com;Port=45371;Database=mot_test;Uid=user;Pwd=pass;")]
    [InlineData("Server=192.168.1.50;Port=3306;Database=mot_test;Uid=user;Pwd=pass;")]
    [InlineData("Server=remote.db.com;Port=3306;Database=mot_test;Uid=user;Pwd=pass;")]
    public async Task SafetyGuard_WhenRemoteHostProvided_ThrowsSecurityException(string remoteConnStr)
    {
        Environment.SetEnvironmentVariable(MySqlTestFixture.EnvironmentGuardVar, "true");
        Environment.SetEnvironmentVariable(MySqlTestFixture.ConnectionStringVar, remoteConnStr);

        var fixture = new MySqlTestFixture();
        Func<Task> act = async () => await fixture.InitializeAsync();

        await act.Should().ThrowAsync<SecurityException>()
            .WithMessage("*CRITICAL SAFETY GUARD REJECTION*Only 'localhost' or '127.0.0.1' is permitted*");

        // Clean up
        Environment.SetEnvironmentVariable(MySqlTestFixture.EnvironmentGuardVar, null);
        Environment.SetEnvironmentVariable(MySqlTestFixture.ConnectionStringVar, null);
        Environment.SetEnvironmentVariable(MySqlTestFixture.AliasConnectionStringVar, null);
    }

    [Theory]
    [InlineData("Server=localhost;Port=3306;Database=mindontrack-31353797c1;Uid=root;Pwd=;")]
    [InlineData("Server=127.0.0.1;Port=3306;Database=production;Uid=root;Pwd=;")]
    [InlineData("Server=localhost;Port=3306;Database=mot_live;Uid=root;Pwd=;")]
    public async Task SafetyGuard_WhenDatabaseNameNotDesignatedForTesting_ThrowsSecurityException(string nonTestConnStr)
    {
        Environment.SetEnvironmentVariable(MySqlTestFixture.EnvironmentGuardVar, "true");
        Environment.SetEnvironmentVariable(MySqlTestFixture.ConnectionStringVar, nonTestConnStr);

        var fixture = new MySqlTestFixture();
        Func<Task> act = async () => await fixture.InitializeAsync();

        await act.Should().ThrowAsync<SecurityException>()
            .WithMessage("*CRITICAL SAFETY GUARD REJECTION*Database name must contain 'test' or 'integration'*");

        // Clean up
        Environment.SetEnvironmentVariable(MySqlTestFixture.EnvironmentGuardVar, null);
        Environment.SetEnvironmentVariable(MySqlTestFixture.ConnectionStringVar, null);
        Environment.SetEnvironmentVariable(MySqlTestFixture.AliasConnectionStringVar, null);
    }

    [Fact]
    public async Task SafetyGuard_WhenAliasConnectionStringUsed_StrictGuardsStillEnforced()
    {
        Environment.SetEnvironmentVariable(MySqlTestFixture.EnvironmentGuardVar, "true");
        Environment.SetEnvironmentVariable(MySqlTestFixture.ConnectionStringVar, null);
        Environment.SetEnvironmentVariable(MySqlTestFixture.AliasConnectionStringVar, "Server=remote.db.com;Port=3306;Database=mot_test;Uid=user;Pwd=pass;");

        var fixture = new MySqlTestFixture();
        Func<Task> act = async () => await fixture.InitializeAsync();

        await act.Should().ThrowAsync<SecurityException>()
            .WithMessage("*CRITICAL SAFETY GUARD REJECTION*Only 'localhost' or '127.0.0.1' is permitted*");

        // Clean up
        Environment.SetEnvironmentVariable(MySqlTestFixture.EnvironmentGuardVar, null);
        Environment.SetEnvironmentVariable(MySqlTestFixture.ConnectionStringVar, null);
        Environment.SetEnvironmentVariable(MySqlTestFixture.AliasConnectionStringVar, null);
    }
}
