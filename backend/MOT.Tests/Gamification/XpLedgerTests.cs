using System;
using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;
using MOT.Domain.Gamification;
using MOT.Infrastructure.Persistence;
using Xunit;

namespace MOT.Tests.Gamification;

public class XpLedgerTests
{
    private static AppDbContext CreateTestDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql("Server=localhost;Database=test_schema;Uid=test;Pwd=test;", ServerVersion.Parse("10.11.18-mariadb"))
            .Options;

        return new AppDbContext(options);
    }

    // =========================================================================
    // 1. Entity and Constants Unit Tests
    // =========================================================================

    [Fact]
    public void XpTransaction_DefaultInstantiation_InitializesIdAndUtcTimestamp()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var transaction = new XpTransaction();
        var after = DateTime.UtcNow.AddSeconds(1);

        transaction.Id.Should().NotBeEmpty();
        transaction.CreatedAt.Should().BeOnOrAfter(before);
        transaction.CreatedAt.Should().BeOnOrBefore(after);
        transaction.Amount.Should().Be(0);
        transaction.SourceType.Should().BeEmpty();
        transaction.SourceId.Should().BeEmpty();
        transaction.Description.Should().BeEmpty();
    }

    [Fact]
    public void XpTransaction_Properties_SetAndGetCorrectly()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        var transaction = new XpTransaction
        {
            Id = id,
            UserId = userId,
            Amount = 25,
            SourceType = XpSourceTypes.StudySession,
            SourceId = "session-guid-123",
            Description = "Completed 25-minute study session",
            CreatedAt = createdAt
        };

        transaction.Id.Should().Be(id);
        transaction.UserId.Should().Be(userId);
        transaction.Amount.Should().Be(25);
        transaction.SourceType.Should().Be("StudySession");
        transaction.SourceId.Should().Be("session-guid-123");
        transaction.Description.Should().Be("Completed 25-minute study session");
        transaction.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void XpSourceTypes_ContainsExpectedDistinctSourceTypes()
    {
        var sourceTypes = new[]
        {
            XpSourceTypes.StudySession,
            XpSourceTypes.Task,
            XpSourceTypes.Habit,
            XpSourceTypes.Achievement,
            XpSourceTypes.Bonus
        };

        sourceTypes.Should().OnlyHaveUniqueItems();
        sourceTypes.Should().Contain(new[] { "StudySession", "Task", "Habit", "Achievement", "Bonus" });
    }

    // =========================================================================
    // 2. EF Core Model Configuration and Index Metadata Tests
    // =========================================================================

    [Fact]
    public void AppDbContext_XpTransaction_HasCascadeDeleteForeignKeyToUser()
    {
        using var context = CreateTestDbContext();
        var model = context.Model;

        var entityType = model.FindEntityType(typeof(XpTransaction));
        entityType.Should().NotBeNull("XpTransaction must be configured in AppDbContext");

        // Primary key
        var primaryKey = entityType!.FindPrimaryKey();
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Select(p => p.Name).Should().Equal("Id");

        // Foreign Key to Users
        var foreignKey = entityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(User));

        foreignKey.Should().NotBeNull("XpTransaction must have a foreign key referencing User");
        foreignKey!.Properties.Select(p => p.Name).Should().Equal("UserId");
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade,
            "Deleting a user must cascade delete all child XP transactions");
    }

    [Fact]
    public void AppDbContext_XpTransaction_HasUniqueCompositeIndexForDeduplication()
    {
        using var context = CreateTestDbContext();
        var entityType = context.Model.FindEntityType(typeof(XpTransaction));
        entityType.Should().NotBeNull();

        // Unique Composite Index: (UserId, SourceType, SourceId)
        var uniqueIndex = entityType!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "UserId", "SourceType", "SourceId" }));

        uniqueIndex.Should().NotBeNull("A composite index on (UserId, SourceType, SourceId) must exist");
        uniqueIndex!.IsUnique.Should().BeTrue("The index on (UserId, SourceType, SourceId) must be unique to prevent duplicate XP awards");
    }

    [Fact]
    public void AppDbContext_XpTransaction_HasHistoryQueryIndex()
    {
        using var context = CreateTestDbContext();
        var entityType = context.Model.FindEntityType(typeof(XpTransaction));
        entityType.Should().NotBeNull();

        // Index: (UserId, CreatedAt)
        var historyIndex = entityType!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "UserId", "CreatedAt" }));

        historyIndex.Should().NotBeNull("An index on (UserId, CreatedAt) must exist for efficient history queries");
    }

    [Fact]
    public void AppDbContext_XpTransaction_ConfiguresFieldMaxLengthsForMariaDbCompatibility()
    {
        using var context = CreateTestDbContext();
        var entityType = context.Model.FindEntityType(typeof(XpTransaction));
        entityType.Should().NotBeNull();

        var sourceTypeProp = entityType!.FindProperty(nameof(XpTransaction.SourceType));
        sourceTypeProp.Should().NotBeNull();
        sourceTypeProp!.GetMaxLength().Should().Be(32);
        sourceTypeProp.IsNullable.Should().BeFalse();

        var sourceIdProp = entityType.FindProperty(nameof(XpTransaction.SourceId));
        sourceIdProp.Should().NotBeNull();
        sourceIdProp!.GetMaxLength().Should().Be(64);
        sourceIdProp.IsNullable.Should().BeFalse();

        var descriptionProp = entityType.FindProperty(nameof(XpTransaction.Description));
        descriptionProp.Should().NotBeNull();
        descriptionProp!.GetMaxLength().Should().Be(200);
        descriptionProp.IsNullable.Should().BeFalse();
    }
}
