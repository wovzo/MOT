using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MOT.Application.Common.Interfaces;
using MOT.Application.StudySessions;
using MOT.Domain.Entities;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.StudySessions;

public class StudySessionHandlerTests
{
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly List<StudySession> _sessions;

    public StudySessionHandlerTests()
    {
        _sessions = new List<StudySession>();
        _dbContextMock = new Mock<IAppDbContext>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        var mockDbSet = MockDbSetHelper.CreateMockDbSet(_sessions);
        _dbContextMock.Setup(c => c.StudySessions).Returns(mockDbSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task StartStudySession_WhenAuthenticated_CreatesSessionForUserWithGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new StartStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new StartStudySessionCommand { Title = "Deep Work Focus" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Deep Work Focus");
        result.IsCompleted.Should().BeFalse();

        _sessions.Should().HaveCount(1);
        var createdSession = _sessions.Single();
        createdSession.UserId.Should().Be(userId);
        createdSession.Title.Should().Be("Deep Work Focus");
        createdSession.IsCompleted.Should().BeFalse();

        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-guid")]
    public async Task StartStudySession_WhenUserIdIsMissingOrInvalid_ThrowsUnauthorizedAccessException(string? invalidUserId)
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);

        var handler = new StartStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new StartStudySessionCommand { Title = "Deep Work Focus" };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _sessions.Should().BeEmpty();
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EndStudySession_WhenOwnSession_CompletesSessionSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var session = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Morning Physics",
            StartTime = DateTime.UtcNow.AddMinutes(-45),
            IsCompleted = false
        };
        _sessions.Add(session);

        var handler = new EndStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new EndStudySessionCommand { SessionId = session.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(session.Id);
        result.IsCompleted.Should().BeTrue();
        result.EndTime.Should().NotBeNull();
        result.DurationMinutes.Should().BeInRange(44, 46);

        session.IsCompleted.Should().BeTrue();
        session.EndTime.Should().NotBeNull();
        session.DurationMinutes.Should().BeInRange(44, 46);

        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EndStudySession_WhenSessionBelongsToAnotherUser_ThrowsNotFoundAndDoesNotModifySession()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userA.ToString());

        var sessionB = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userB,
            Title = "User B's Private Session",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            IsCompleted = false
        };
        _sessions.Add(sessionB);

        var handler = new EndStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new EndStudySessionCommand { SessionId = sessionB.Id };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Session not found");

        sessionB.IsCompleted.Should().BeFalse();
        sessionB.EndTime.Should().BeNull();
        sessionB.DurationMinutes.Should().Be(0);

        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStudySessions_ReturnsOnlyAuthenticatedUsersSessions()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userA.ToString());

        var sessionA1 = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userA,
            Title = "User A - Math",
            StartTime = DateTime.UtcNow.AddHours(-2),
            IsCompleted = true
        };
        var sessionA2 = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userA,
            Title = "User A - Biology",
            StartTime = DateTime.UtcNow.AddHours(-1),
            IsCompleted = false
        };
        var sessionB = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userB,
            Title = "User B - Chemistry",
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            IsCompleted = false
        };

        _sessions.AddRange(new[] { sessionA1, sessionA2, sessionB });

        var handler = new GetStudySessionsQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var query = new GetStudySessionsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Select(s => s.Id).Should().Contain(new[] { sessionA1.Id, sessionA2.Id });
        result.Select(s => s.Id).Should().NotContain(sessionB.Id);
    }

    [Fact]
    public async Task ClientInput_CannotOverrideAuthenticatedUserId()
    {
        // Arrange
        var authenticatedUser = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(authenticatedUser.ToString());

        // Assert contract: Commands and queries must not expose client-controllable UserId properties
        typeof(StartStudySessionCommand).GetProperty("UserId").Should().BeNull(
            "StartStudySessionCommand must not allow client to submit a UserId property");
        typeof(EndStudySessionCommand).GetProperty("UserId").Should().BeNull(
            "EndStudySessionCommand must not allow client to submit a UserId property");
        typeof(GetStudySessionsQuery).GetProperty("UserId").Should().BeNull(
            "GetStudySessionsQuery must not allow client to submit a UserId property");

        // Verify handler derives ownership exclusively from ICurrentUserService
        var handler = new StartStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new StartStudySessionCommand { Title = "Tamper Proof Test" };

        var result = await handler.Handle(command, CancellationToken.None);

        _sessions.Single().UserId.Should().Be(authenticatedUser);
    }
}
