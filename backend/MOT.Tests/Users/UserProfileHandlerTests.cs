using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.DTOs;
using MOT.Application.Users.Queries;
using MOT.Domain.Entities;
using MOT.Domain.Interfaces;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Users;

public class UserProfileHandlerTests
{
    private readonly Mock<IAuthRepository> _authRepositoryMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IAppDbContext> _dbContextMock;
    private readonly List<StudySession> _sessions;

    public UserProfileHandlerTests()
    {
        _authRepositoryMock = new Mock<IAuthRepository>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _sessions = new List<StudySession>();
        _dbContextMock = new Mock<IAppDbContext>();

        var mockDbSet = MockDbSetHelper.CreateMockDbSet(_sessions);
        _dbContextMock.Setup(c => c.StudySessions).Returns(mockDbSet.Object);
    }

    // ==========================================
    // 1. Handler Tests
    // ==========================================

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsOwnProfileWithAllFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var user = new User
        {
            Id = userId,
            Email = "alex@example.com",
            DisplayName = "Alex Morgan",
            PasswordHash = "super_secret_hash_value",
            CreatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            CurrentStreak = 7,
            Level = 3,
            XP = 450
        };

        _authRepositoryMock
            .Setup(r => r.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _sessions.Add(new StudySession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Morning Focus",
            StartTime = DateTime.UtcNow.AddMinutes(-60),
            EndTime = DateTime.UtcNow,
            DurationMinutes = 60,
            IsCompleted = true
        });

        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object, _dbContextMock.Object);

        // Act
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Email.Should().Be("alex@example.com");
        result.DisplayName.Should().Be("Alex Morgan");
        result.CreatedAt.Should().Be(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        result.CurrentStreak.Should().Be(7);
        result.Level.Should().Be(3);
        result.XP.Should().Be(450);
        result.TotalFocusMinutes.Should().Be(60);
    }

    [Fact]
    public async Task GetCurrentUser_WhenNoCompletedSessions_ReturnsZeroTotalFocusMinutes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var user = new User
        {
            Id = userId,
            Email = "newbie@example.com",
            DisplayName = "New User",
            CreatedAt = DateTime.UtcNow,
            CurrentStreak = 0,
            Level = 1,
            XP = 0
        };

        _authRepositoryMock
            .Setup(r => r.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object, _dbContextMock.Object);

        // Act
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalFocusMinutes.Should().Be(0);
    }

    [Fact]
    public async Task GetCurrentUser_WhenMultipleCompletedSessions_ReturnsSumOfDurations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var user = new User
        {
            Id = userId,
            Email = "focuspro@example.com",
            DisplayName = "Focus Pro",
            CreatedAt = DateTime.UtcNow,
            CurrentStreak = 5,
            Level = 2,
            XP = 200
        };

        _authRepositoryMock
            .Setup(r => r.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _sessions.AddRange(new[]
        {
            new StudySession { Id = Guid.NewGuid(), UserId = userId, Title = "Session 1", DurationMinutes = 30, StartTime = DateTime.UtcNow.AddMinutes(-200), EndTime = DateTime.UtcNow.AddMinutes(-170), IsCompleted = true },
            new StudySession { Id = Guid.NewGuid(), UserId = userId, Title = "Session 2", DurationMinutes = 45, StartTime = DateTime.UtcNow.AddMinutes(-150), EndTime = DateTime.UtcNow.AddMinutes(-105), IsCompleted = true },
            new StudySession { Id = Guid.NewGuid(), UserId = userId, Title = "Session 3", DurationMinutes = 60, StartTime = DateTime.UtcNow.AddMinutes(-80), EndTime = DateTime.UtcNow.AddMinutes(-20), IsCompleted = true }
        });

        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object, _dbContextMock.Object);

        // Act
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalFocusMinutes.Should().Be(135);
    }

    [Fact]
    public async Task GetCurrentUser_WhenActiveSessionPresent_ExcludesActiveSessionFromTotalFocusMinutes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var user = new User
        {
            Id = userId,
            Email = "activeuser@example.com",
            DisplayName = "Active User",
            CreatedAt = DateTime.UtcNow,
            CurrentStreak = 1,
            Level = 1,
            XP = 50
        };

        _authRepositoryMock
            .Setup(r => r.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _sessions.AddRange(new[]
        {
            new StudySession { Id = Guid.NewGuid(), UserId = userId, Title = "Completed 1", DurationMinutes = 30, StartTime = DateTime.UtcNow.AddMinutes(-60), EndTime = DateTime.UtcNow.AddMinutes(-30), IsCompleted = true },
            new StudySession { Id = Guid.NewGuid(), UserId = userId, Title = "Ongoing Active", DurationMinutes = 0, StartTime = DateTime.UtcNow.AddMinutes(-15), EndTime = null, IsCompleted = false }
        });

        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object, _dbContextMock.Object);

        // Act
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalFocusMinutes.Should().Be(30);
    }

    [Fact]
    public async Task GetCurrentUser_WhenOtherUsersSessionsPresent_ExcludesOtherUsersSessions()
    {
        // Arrange
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        _currentUserServiceMock.Setup(s => s.UserId).Returns(userAId.ToString());

        var userA = new User
        {
            Id = userAId,
            Email = "usera@example.com",
            DisplayName = "User A",
            CreatedAt = DateTime.UtcNow,
            CurrentStreak = 1,
            Level = 1,
            XP = 30
        };

        _authRepositoryMock
            .Setup(r => r.GetUserByIdAsync(userAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userA);

        _sessions.AddRange(new[]
        {
            new StudySession { Id = Guid.NewGuid(), UserId = userAId, Title = "User A Focus", DurationMinutes = 30, StartTime = DateTime.UtcNow.AddMinutes(-60), EndTime = DateTime.UtcNow.AddMinutes(-30), IsCompleted = true },
            new StudySession { Id = Guid.NewGuid(), UserId = userBId, Title = "User B Heavy Focus", DurationMinutes = 500, StartTime = DateTime.UtcNow.AddMinutes(-600), EndTime = DateTime.UtcNow.AddMinutes(-100), IsCompleted = true }
        });

        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object, _dbContextMock.Object);

        // Act
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalFocusMinutes.Should().Be(30);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetCurrentUser_WhenUserIdIsMissing_ThrowsUnauthorizedAccessException(string? missingUserId)
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.UserId).Returns(missingUserId);
        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object, _dbContextMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _authRepositoryMock.Verify(r => r.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    public async Task GetCurrentUser_WhenUserIdIsInvalidGuid_ThrowsUnauthorizedAccessException(string invalidUserId)
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);
        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object, _dbContextMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _authRepositoryMock.Verify(r => r.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserNotFoundInDatabase_ThrowsUserNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        _authRepositoryMock
            .Setup(r => r.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object, _dbContextMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UserNotFoundException>()
            .WithMessage($"*{userId}*");
    }

    [Fact]
    public void UserProfileDto_DoesNotExposeSensitiveAuthenticationData()
    {
        // Assert that UserProfileDto does not expose password hashes or auth secrets
        var propertyNames = typeof(UserProfileDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        propertyNames.Should().NotContain(p => p.Name.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0);
        propertyNames.Should().NotContain(p => p.Name.IndexOf("Hash", StringComparison.OrdinalIgnoreCase) >= 0);
        propertyNames.Should().NotContain(p => p.Name.IndexOf("Secret", StringComparison.OrdinalIgnoreCase) >= 0);
        propertyNames.Should().NotContain(p => p.Name.IndexOf("Token", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void GetCurrentUserQuery_HasNoClientUserIdProperty()
    {
        // Assert that client request cannot supply UserId through query
        var properties = typeof(GetCurrentUserQuery).GetProperties();
        properties.Should().NotContain(p => p.Name.Equals("UserId", StringComparison.OrdinalIgnoreCase));
    }

    // ==========================================
    // 2. Controller Endpoint & HTTP Semantics
    // ==========================================

    [Fact]
    public async Task AuthController_GetCurrentUser_WhenValid_Returns200WithProfileAndTotalFocusMinutes()
    {
        // Arrange
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();

        var userId = Guid.NewGuid();
        var profileDto = new UserProfileDto(
            userId,
            "sam@example.com",
            "Sam Wilson",
            DateTime.UtcNow,
            10,
            2,
            250,
            150
        );

        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetCurrentUserQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDto);

        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetCurrentUser();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(profileDto);

        var returnedDto = okResult.Value as UserProfileDto;
        returnedDto.Should().NotBeNull();
        returnedDto!.TotalFocusMinutes.Should().Be(150);
    }

    [Fact]
    public async Task AuthController_GetCurrentUser_WhenUserNotFound_Returns404NotFound()
    {
        // Arrange
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var userId = Guid.NewGuid();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetCurrentUserQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UserNotFoundException(userId));

        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetCurrentUser();

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);

        var errorProp = notFoundResult.Value?.GetType().GetProperty("error")?.GetValue(notFoundResult.Value)?.ToString();
        errorProp.Should().Be($"User with ID '{userId}' was not found.");
    }

    [Fact]
    public async Task AuthController_GetCurrentUser_WhenUnexpectedExceptionOccurs_Returns500AndHidesInternalDetails()
    {
        // Arrange
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();

        mediatorMock
            .Setup(m => m.Send(It.IsAny<GetCurrentUserQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Sensitive MySQL database stack trace"));

        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        // Act
        var result = await controller.GetCurrentUser();

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);

        var errorProp = statusResult.Value?.GetType().GetProperty("error")?.GetValue(statusResult.Value)?.ToString();
        errorProp.Should().NotContain("MySQL");
        errorProp.Should().NotContain("Sensitive");
        errorProp.Should().Be("An unexpected error occurred while retrieving user profile.");

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

