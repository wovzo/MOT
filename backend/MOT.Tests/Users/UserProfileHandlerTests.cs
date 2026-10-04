using System;
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
using Xunit;

namespace MOT.Tests.Users;

public class UserProfileHandlerTests
{
    private readonly Mock<IAuthRepository> _authRepositoryMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;

    public UserProfileHandlerTests()
    {
        _authRepositoryMock = new Mock<IAuthRepository>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
    }

    // ==========================================
    // 1. Handler Tests
    // ==========================================

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsOwnProfile()
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

        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object);

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
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetCurrentUser_WhenUserIdIsMissing_ThrowsUnauthorizedAccessException(string? missingUserId)
    {
        // Arrange
        _currentUserServiceMock.Setup(s => s.UserId).Returns(missingUserId);
        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object);

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
        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object);

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

        var handler = new GetCurrentUserQueryHandler(_authRepositoryMock.Object, _currentUserServiceMock.Object);

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
    public async Task AuthController_GetCurrentUser_WhenValid_Returns200WithProfile()
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
            250
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
