using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;
using MOT.IntegrationTests.Fixtures;
using Xunit;

namespace MOT.IntegrationTests.Constraints;

[Trait("Category", "DatabaseIntegration")]
public class SubjectConstraintTests : IClassFixture<MySqlTestFixture>
{
    private readonly MySqlTestFixture _fixture;

    public SubjectConstraintTests(MySqlTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Subject_DuplicateNameForSameUser_ThrowsDbUpdateException()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"sub_{Guid.NewGuid():N}@test.com", DisplayName = "Subject Tester" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var subject1 = new Subject { Id = Guid.NewGuid(), UserId = user.Id, Name = "Mathematics" };
        context.Subjects.Add(subject1);
        await context.SaveChangesAsync();

        var subject2 = new Subject { Id = Guid.NewGuid(), UserId = user.Id, Name = "Mathematics" };
        context.Subjects.Add(subject2);

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("Oracle MySQL unique constraint on {UserId, Name} must prevent duplicate subjects for the same user");
    }

    [Fact]
    public async Task Subject_CaseInsensitiveCollation_CollidesOnDifferentCasing()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"case_{Guid.NewGuid():N}@test.com", DisplayName = "Case Tester" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var subject1 = new Subject { Id = Guid.NewGuid(), UserId = user.Id, Name = "Physics" };
        context.Subjects.Add(subject1);
        await context.SaveChangesAsync();

        // In Oracle MySQL 8.0 default utf8mb4_0900_ai_ci collation, "physics" collides with "Physics"
        var subject2 = new Subject { Id = Guid.NewGuid(), UserId = user.Id, Name = "physics" };
        context.Subjects.Add(subject2);

        Func<Task> act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("MySQL 8.0 CI collation treats 'Physics' and 'physics' as duplicates");
    }

    [Fact]
    public async Task Subject_WhenDeleted_NullifiesSubjectIdOnLinkedTasksAndStudySessions()
    {
        _fixture.EnsureAvailable();

        using var context = _fixture.CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = $"del_{Guid.NewGuid():N}@test.com", DisplayName = "Delete Tester" };
        context.Users.Add(user);

        var subject = new Subject { Id = Guid.NewGuid(), UserId = user.Id, Name = "Chemistry" };
        context.Subjects.Add(subject);

        var task = new DailyTask { Id = Guid.NewGuid(), UserId = user.Id, SubjectId = subject.Id, Title = "Solve Lab Report" };
        context.Tasks.Add(task);

        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SubjectId = subject.Id,
            Title = "Chemistry Study",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow,
            IsCompleted = true,
            DurationMinutes = 60
        };
        context.StudySessions.Add(session);
        await context.SaveChangesAsync();

        // Act: Delete Subject
        context.Subjects.Remove(subject);
        await context.SaveChangesAsync();

        // Assert: Subject is deleted, but task and session remain with SubjectId = null (DeleteBehavior.SetNull)
        using var verifyContext = _fixture.CreateDbContext();
        var reloadedTask = await verifyContext.Tasks.FindAsync(task.Id);
        var reloadedSession = await verifyContext.StudySessions.FindAsync(session.Id);

        reloadedTask.Should().NotBeNull();
        reloadedTask!.SubjectId.Should().BeNull("Deleting subject must set linked task SubjectId to NULL");

        reloadedSession.Should().NotBeNull();
        reloadedSession!.SubjectId.Should().BeNull("Deleting subject must set linked session SubjectId to NULL");
    }
}
