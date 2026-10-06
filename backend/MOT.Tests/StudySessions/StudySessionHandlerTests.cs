using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Common.Exceptions;
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
        await act.Should().ThrowAsync<StudySessionNotFoundException>().WithMessage($"*{sessionB.Id}*");

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

    // ==========================================
    // P0.2 - Active Study Session Query Tests
    // ==========================================

    [Fact]
    public async Task GetActiveStudySession_WhenActiveSessionExists_ReturnsSessionForUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var activeSession = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Active Session",
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            IsCompleted = false
        };
        var completedSession = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Old Completed Session",
            StartTime = DateTime.UtcNow.AddHours(-2),
            EndTime = DateTime.UtcNow.AddHours(-1),
            DurationMinutes = 60,
            IsCompleted = true
        };
        _sessions.AddRange(new[] { completedSession, activeSession });

        var handler = new GetActiveStudySessionQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetActiveStudySessionQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(activeSession.Id);
        result.Title.Should().Be("Active Session");
        result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveStudySession_WhenNoActiveSession_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var completedSession = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Completed Session",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 60,
            IsCompleted = true
        };
        _sessions.Add(completedSession);

        var handler = new GetActiveStudySessionQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetActiveStudySessionQuery(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveStudySession_DoesNotReturnOtherUsersActiveSession()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userA.ToString());

        var userBActiveSession = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userB,
            Title = "User B Active",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            IsCompleted = false
        };
        _sessions.Add(userBActiveSession);

        var handler = new GetActiveStudySessionQueryHandler(_dbContextMock.Object, _currentUserServiceMock.Object);

        // Act
        var result = await handler.Handle(new GetActiveStudySessionQuery(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ==========================================
    // P0.2 - Validation Tests
    // ==========================================

    [Theory]
    [InlineData("Valid Session Title")]
    [InlineData("Deep Work")]
    public void StartStudySessionValidator_WithValidTitle_PassesValidation(string title)
    {
        var validator = new StartStudySessionCommandValidator();
        var command = new StartStudySessionCommand { Title = title };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void StartStudySessionValidator_WithEmptyTitle_FailsValidation(string? emptyTitle)
    {
        var validator = new StartStudySessionCommandValidator();
        var command = new StartStudySessionCommand { Title = emptyTitle! };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void StartStudySessionValidator_WithTooLongTitle_FailsValidation()
    {
        var validator = new StartStudySessionCommandValidator();
        var command = new StartStudySessionCommand { Title = new string('A', 101) };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    // ==========================================
    // P0.2 - Error Semantics & Controller Tests
    // ==========================================

    [Fact]
    public async Task EndStudySession_WhenNotFound_ThrowsStudySessionNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());
        var nonExistentId = Guid.NewGuid();

        var handler = new EndStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new EndStudySessionCommand { SessionId = nonExistentId };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<StudySessionNotFoundException>();
        ex.WithMessage($"*{nonExistentId}*");
    }

    [Fact]
    public async Task StudySessionsController_GetActive_WhenSessionExists_ReturnsOkResult()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<StudySessionsController>>();
        var expectedDto = new StudySessionDto
        {
            Id = Guid.NewGuid(),
            Title = "Active Session",
            StartTime = DateTime.UtcNow,
            IsCompleted = false
        };
        mediatorMock.Setup(m => m.Send(It.IsAny<GetActiveStudySessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var controller = new StudySessionsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetActiveStudySession();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedDto);
    }

    [Fact]
    public async Task StudySessionsController_GetActive_WhenNoSession_ReturnsNoContentResult()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<StudySessionsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<GetActiveStudySessionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StudySessionDto?)null);

        var controller = new StudySessionsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetActiveStudySession();

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task StudySessionsController_EndSession_WhenNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<StudySessionsController>>();
        var sessionId = Guid.NewGuid();
        mediatorMock.Setup(m => m.Send(It.IsAny<EndStudySessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StudySessionNotFoundException(sessionId));

        var controller = new StudySessionsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.EndSession(sessionId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task StudySessionsController_StartSession_WhenValidationFails_ReturnsBadRequestResult()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<StudySessionsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<StartStudySessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FluentValidation.ValidationException(new[] { new ValidationFailure("Title", "Title is required.") }));

        var controller = new StudySessionsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.StartSession(new StartStudySessionCommand { Title = "" });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task StudySessionsController_WhenUnexpectedExceptionOccurs_ReturnsStatusCode500()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<StudySessionsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<GetStudySessionsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        var controller = new StudySessionsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetStudySessions();

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    // ==========================================
    // P0.7 - Single Active Session Concurrency Tests
    // ==========================================

    [Fact]
    public async Task StartStudySession_WhenActiveSessionAlreadyExists_ThrowsActiveStudySessionAlreadyExistsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var existingActiveSession = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Existing Active Session",
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            IsCompleted = false,
            EndTime = null
        };
        _sessions.Add(existingActiveSession);

        var handler = new StartStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new StartStudySessionCommand { Title = "Second Session Attempt" };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ActiveStudySessionAlreadyExistsException>()
            .WithMessage("You already have an active study session.");

        _sessions.Should().HaveCount(1);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartStudySession_WhenCompletedSessionsExist_AllowsStartingNewSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var completedSession1 = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Yesterday Session",
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(-1).AddHours(1),
            DurationMinutes = 60,
            IsCompleted = true
        };
        var completedSession2 = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Morning Session",
            StartTime = DateTime.UtcNow.AddHours(-3),
            EndTime = DateTime.UtcNow.AddHours(-2),
            DurationMinutes = 60,
            IsCompleted = true
        };
        _sessions.AddRange(new[] { completedSession1, completedSession2 });

        var handler = new StartStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new StartStudySessionCommand { Title = "New Active Session" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("New Active Session");
        result.IsCompleted.Should().BeFalse();
        _sessions.Should().HaveCount(3);
        _sessions.Count(s => !s.IsCompleted).Should().Be(1);
    }

    [Fact]
    public async Task StartStudySession_WhenDifferentUserHasActiveSession_AllowsStartingSessionForCurrentUser()
    {
        // Arrange
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userA.ToString());

        var userBActiveSession = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userB,
            Title = "User B Active",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            IsCompleted = false,
            EndTime = null
        };
        _sessions.Add(userBActiveSession);

        var handler = new StartStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new StartStudySessionCommand { Title = "User A Active" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("User A Active");
        _sessions.Should().HaveCount(2);
        _sessions.Count(s => !s.IsCompleted).Should().Be(2);
    }

    [Fact]
    public async Task StartStudySession_EndSessionThenStart_AllowsStartingNewSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var activeSession = new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Initial Active Session",
            StartTime = DateTime.UtcNow.AddMinutes(-40),
            IsCompleted = false,
            EndTime = null
        };
        _sessions.Add(activeSession);

        var endHandler = new EndStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        await endHandler.Handle(new EndStudySessionCommand { SessionId = activeSession.Id }, CancellationToken.None);

        var startHandler = new StartStudySessionCommandHandler(_dbContextMock.Object, _currentUserServiceMock.Object);
        var command = new StartStudySessionCommand { Title = "Next Active Session" };

        // Act
        var result = await startHandler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Next Active Session");
        _sessions.Count(s => !s.IsCompleted).Should().Be(1);
        _sessions.Count(s => s.IsCompleted).Should().Be(1);
    }

    [Fact]
    public async Task StudySessionsController_StartSession_WhenActiveSessionAlreadyExists_ReturnsConflict409()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<StudySessionsController>>();
        mediatorMock.Setup(m => m.Send(It.IsAny<StartStudySessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ActiveStudySessionAlreadyExistsException());

        var controller = new StudySessionsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.StartSession(new StartStudySessionCommand { Title = "Duplicate Active" });

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task StudySessionsController_StartSession_WhenDbUpdateExceptionIsActiveUserIdViolation_ReturnsConflict409()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<StudySessionsController>>();
        var innerException = new Exception("Duplicate entry 'guid-123' for key 'IX_StudySessions_ActiveUserId' (Error 1062)");
        var dbUpdateException = new DbUpdateException("An error occurred while saving the entity changes.", innerException);

        mediatorMock.Setup(m => m.Send(It.IsAny<StartStudySessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbUpdateException);

        var controller = new StudySessionsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.StartSession(new StartStudySessionCommand { Title = "Concurrent Duplicate Active" });

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task StudySessionsController_StartSession_WhenDbUpdateExceptionIsUnrelated_ReturnsStatusCode500()
    {
        // Arrange
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<StudySessionsController>>();
        var dbUpdateException = new DbUpdateException("Unrelated database constraint violation.", new Exception("Foreign key failure"));

        mediatorMock.Setup(m => m.Send(It.IsAny<StartStudySessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbUpdateException);

        var controller = new StudySessionsController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.StartSession(new StartStudySessionCommand { Title = "Failed Session" });

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }
}


