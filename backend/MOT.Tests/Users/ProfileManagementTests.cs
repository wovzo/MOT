using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.DTOs;
using MOT.Application.Users.Commands;
using MOT.Application.Validators;
using MOT.Domain.Entities;
using MOT.Domain.Interfaces;
using MOT.Infrastructure.Authentication;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Users;

public class ProfileManagementTests
{
    private readonly IPasswordHasher _passwordHasher;

    public ProfileManagementTests()
    {
        _passwordHasher = new PasswordHasher();
    }

    // =========================================================================
    // 1. Profile Retrieval & Editing Tests
    // =========================================================================

    [Fact]
    public async Task UpdateProfile_WhenValid_UpdatesDisplayNameAndReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "alex@example.com",
            DisplayName = "Alex OldName",
            PasswordHash = _passwordHasher.HashPassword("SecurePassword123!"),
            CreatedAt = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
            XP = 250,
            Level = 3,
            CurrentStreak = 5
        };

        var users = new List<User> { user };
        var sessions = new List<StudySession>
        {
            new StudySession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Math Focus",
                DurationMinutes = 45,
                StartTime = DateTime.UtcNow.AddMinutes(-60),
                EndTime = DateTime.UtcNow.AddMinutes(-15),
                IsCompleted = true
            }
        };

        var dbContextMock = new Mock<IAppDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);
        dbContextMock.Setup(c => c.StudySessions).Returns(MockDbSetHelper.CreateMockDbSet(sessions).Object);
        dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new UpdateProfileCommandHandler(dbContextMock.Object, currentUserServiceMock.Object);
        var command = new UpdateProfileCommand { DisplayName = "  Alex Updated  " };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Alex Updated"); // Trimmed
        user.DisplayName.Should().Be("Alex Updated");
        result.Email.Should().Be("alex@example.com");
        result.Id.Should().Be(userId);
        result.XP.Should().Be(250);
        result.Level.Should().Be(3);
        result.CurrentStreak.Should().Be(5);
        result.TotalFocusMinutes.Should().Be(45);
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_PreservesAllProtectedFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var originalCreatedAt = new DateTime(2025, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        var originalPasswordHash = _passwordHasher.HashPassword("OriginalPass123!");

        var user = new User
        {
            Id = userId,
            Email = "protected@example.com",
            DisplayName = "Original Name",
            PasswordHash = originalPasswordHash,
            CreatedAt = originalCreatedAt,
            XP = 1200,
            Level = 8,
            CurrentStreak = 14
        };

        var users = new List<User> { user };
        var sessions = new List<StudySession>();

        var dbContextMock = new Mock<IAppDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);
        dbContextMock.Setup(c => c.StudySessions).Returns(MockDbSetHelper.CreateMockDbSet(sessions).Object);
        dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new UpdateProfileCommandHandler(dbContextMock.Object, currentUserServiceMock.Object);
        var command = new UpdateProfileCommand { DisplayName = "New Display Name" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert that protected fields remain identical
        user.Id.Should().Be(userId);
        user.Email.Should().Be("protected@example.com");
        user.PasswordHash.Should().Be(originalPasswordHash);
        user.CreatedAt.Should().Be(originalCreatedAt);
        user.XP.Should().Be(1200);
        user.Level.Should().Be(8);
        user.CurrentStreak.Should().Be(14);

        result.Id.Should().Be(userId);
        result.Email.Should().Be("protected@example.com");
        result.CreatedAt.Should().Be(originalCreatedAt);
        result.XP.Should().Be(1200);
        result.Level.Should().Be(8);
        result.CurrentStreak.Should().Be(14);
    }

    [Fact]
    public async Task UpdateProfile_CrossUserIsolation_OnlyUpdatesAuthenticatedUser()
    {
        // Arrange
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        var userA = new User { Id = userAId, Email = "usera@example.com", DisplayName = "User A Original" };
        var userB = new User { Id = userBId, Email = "userb@example.com", DisplayName = "User B Original" };

        var users = new List<User> { userA, userB };
        var sessions = new List<StudySession>();

        var dbContextMock = new Mock<IAppDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);
        dbContextMock.Setup(c => c.StudySessions).Returns(MockDbSetHelper.CreateMockDbSet(sessions).Object);
        dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(userAId.ToString());

        var handler = new UpdateProfileCommandHandler(dbContextMock.Object, currentUserServiceMock.Object);
        var command = new UpdateProfileCommand { DisplayName = "User A Changed" };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        userA.DisplayName.Should().Be("User A Changed");
        userB.DisplayName.Should().Be("User B Original"); // User B completely unaffected
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-guid")]
    public async Task UpdateProfile_WhenUnauthenticatedOrInvalidUserId_ThrowsUnauthorizedAccessException(string? invalidUserId)
    {
        // Arrange
        var dbContextMock = new Mock<IAppDbContext>();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);

        var handler = new UpdateProfileCommandHandler(dbContextMock.Object, currentUserServiceMock.Object);
        var command = new UpdateProfileCommand { DisplayName = "Valid Name" };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateProfile_WhenUserNotFound_ThrowsUserNotFoundException()
    {
        // Arrange
        var missingUserId = Guid.NewGuid();
        var users = new List<User>();
        var sessions = new List<StudySession>();

        var dbContextMock = new Mock<IAppDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);
        dbContextMock.Setup(c => c.StudySessions).Returns(MockDbSetHelper.CreateMockDbSet(sessions).Object);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(missingUserId.ToString());

        var handler = new UpdateProfileCommandHandler(dbContextMock.Object, currentUserServiceMock.Object);
        var command = new UpdateProfileCommand { DisplayName = "Valid Name" };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UserNotFoundException>()
            .WithMessage($"*{missingUserId}*");
    }

    [Fact]
    public void UpdateProfileCommand_HasNoProtectedProperties()
    {
        // Ensure client payload cannot contain protected fields
        var properties = typeof(UpdateProfileCommand).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propertyNames = properties.Select(p => p.Name).ToList();

        propertyNames.Should().ContainSingle().Which.Should().Be("DisplayName");
        propertyNames.Should().NotContain("Id");
        propertyNames.Should().NotContain("UserId");
        propertyNames.Should().NotContain("Email");
        propertyNames.Should().NotContain("Password");
        propertyNames.Should().NotContain("PasswordHash");
        propertyNames.Should().NotContain("XP");
        propertyNames.Should().NotContain("Level");
        propertyNames.Should().NotContain("CurrentStreak");
        propertyNames.Should().NotContain("CreatedAt");
        propertyNames.Should().NotContain("Role");
    }

    [Theory]
    [InlineData("Valid Name", true)]
    [InlineData("A", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void UpdateProfileValidator_ValidatesDisplayName(string displayName, bool isValid)
    {
        var validator = new UpdateProfileCommandValidator();
        var result = validator.Validate(new UpdateProfileCommand { DisplayName = displayName });
        result.IsValid.Should().Be(isValid);
    }

    [Fact]
    public void UpdateProfileValidator_WhenDisplayNameExceeds100Chars_Fails()
    {
        var validator = new UpdateProfileCommandValidator();
        var longName = new string('x', 101);
        var result = validator.Validate(new UpdateProfileCommand { DisplayName = longName });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayName" && e.ErrorMessage.Contains("100"));
    }

    // =========================================================================
    // 2. Password Change Regression Tests
    // =========================================================================

    [Fact]
    public async Task ChangePassword_WhenCurrentPasswordIncorrect_PreservesOriginalPasswordHash()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var initialHash = _passwordHasher.HashPassword("InitialCorrectPass123!");
        var user = new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = initialHash
        };

        var users = new List<User> { user };
        var dbContextMock = new Mock<IAppDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new ChangePasswordCommandHandler(dbContextMock.Object, currentUserServiceMock.Object, _passwordHasher);
        var command = new ChangePasswordCommand { CurrentPassword = "WrongPasswordGuess!", NewPassword = "BrandNewPass123!" };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();
        user.PasswordHash.Should().Be(initialHash); // Unchanged!
        _passwordHasher.VerifyPassword("InitialCorrectPass123!", user.PasswordHash).Should().BeTrue();
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ChangePasswordValidator_WhenNewPasswordEqualsCurrent_Rejects()
    {
        var validator = new ChangePasswordCommandValidator();
        var command = new ChangePasswordCommand { CurrentPassword = "SamePassword123!", NewPassword = "SamePassword123!" };
        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("different"));
    }

    // =========================================================================
    // 3. Account Deletion Tests
    // =========================================================================

    [Fact]
    public async Task DeleteAccount_WhenValidPassword_RemovesUserAndRoomParticipants()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "delete_me@example.com",
            PasswordHash = _passwordHasher.HashPassword("MySecretPass123!")
        };

        var users = new List<User> { user };
        var participants = new List<RoomParticipant>
        {
            new RoomParticipant
            {
                Id = Guid.NewGuid(),
                RoomName = "Study_Room_1",
                UserId = userId,
                Username = "delete_me",
                SeatNumber = 5,
                JoinedAt = DateTime.UtcNow,
                LastPing = DateTime.UtcNow
            },
            new RoomParticipant
            {
                Id = Guid.NewGuid(),
                RoomName = "Study_Room_1",
                UserId = Guid.NewGuid(), // Other user
                Username = "other_user",
                SeatNumber = 6,
                JoinedAt = DateTime.UtcNow,
                LastPing = DateTime.UtcNow
            }
        };

        var mockUsersSet = MockDbSetHelper.CreateMockDbSet(users);
        mockUsersSet.Setup(m => m.Remove(It.IsAny<User>())).Callback<User>(u => users.Remove(u));

        var mockParticipantsSet = MockDbSetHelper.CreateMockDbSet(participants);
        mockParticipantsSet.Setup(m => m.RemoveRange(It.IsAny<IEnumerable<RoomParticipant>>()))
            .Callback<IEnumerable<RoomParticipant>>(items =>
            {
                foreach (var item in items.ToList())
                    participants.Remove(item);
            });

        var dbContextMock = new Mock<IAppDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(mockUsersSet.Object);
        dbContextMock.Setup(c => c.RoomParticipants).Returns(mockParticipantsSet.Object);
        dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new DeleteAccountCommandHandler(dbContextMock.Object, currentUserServiceMock.Object, _passwordHasher);
        var command = new DeleteAccountCommand { Password = "MySecretPass123!" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        users.Should().NotContain(u => u.Id == userId);
        participants.Should().NotContain(p => p.UserId == userId);
        participants.Should().HaveCount(1); // Other user's participant remains intact
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAccount_WhenPasswordIncorrect_ThrowsInvalidCredentialsAndPreservesUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "keeper@example.com",
            PasswordHash = _passwordHasher.HashPassword("CorrectPassword123!")
        };

        var users = new List<User> { user };
        var participants = new List<RoomParticipant>
        {
            new RoomParticipant { Id = Guid.NewGuid(), RoomName = "R1", UserId = userId, Username = "keeper", SeatNumber = 1 }
        };

        var mockUsersSet = MockDbSetHelper.CreateMockDbSet(users);
        var mockParticipantsSet = MockDbSetHelper.CreateMockDbSet(participants);

        var dbContextMock = new Mock<IAppDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(mockUsersSet.Object);
        dbContextMock.Setup(c => c.RoomParticipants).Returns(mockParticipantsSet.Object);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(userId.ToString());

        var handler = new DeleteAccountCommandHandler(dbContextMock.Object, currentUserServiceMock.Object, _passwordHasher);
        var command = new DeleteAccountCommand { Password = "WrongPassword123!" };

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<InvalidCredentialsException>();
        ex.WithMessage("Password is incorrect.");

        users.Should().Contain(u => u.Id == userId); // User preserved!
        participants.Should().Contain(p => p.UserId == userId); // Room participant preserved!
        mockUsersSet.Verify(m => m.Remove(It.IsAny<User>()), Times.Never);
        mockParticipantsSet.Verify(m => m.RemoveRange(It.IsAny<IEnumerable<RoomParticipant>>()), Times.Never);
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task DeleteAccount_WhenUnauthenticated_ThrowsUnauthorizedAccessException(string? invalidUserId)
    {
        var dbContextMock = new Mock<IAppDbContext>();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(invalidUserId);

        var handler = new DeleteAccountCommandHandler(dbContextMock.Object, currentUserServiceMock.Object, _passwordHasher);
        var command = new DeleteAccountCommand { Password = "AnyPassword123!" };

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteAccount_WhenUserNotFound_ThrowsUserNotFoundException()
    {
        var missingUserId = Guid.NewGuid();
        var users = new List<User>();

        var dbContextMock = new Mock<IAppDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(missingUserId.ToString());

        var handler = new DeleteAccountCommandHandler(dbContextMock.Object, currentUserServiceMock.Object, _passwordHasher);
        var command = new DeleteAccountCommand { Password = "Password123!" };

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UserNotFoundException>()
            .WithMessage($"*{missingUserId}*");
    }

    [Fact]
    public void DeleteAccountCommand_HasNoTargetUserIdProperty()
    {
        // Assert that client payload cannot specify a target user ID
        var properties = typeof(DeleteAccountCommand).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propertyNames = properties.Select(p => p.Name).ToList();

        propertyNames.Should().ContainSingle().Which.Should().Be("Password");
        propertyNames.Should().NotContain("UserId");
        propertyNames.Should().NotContain("TargetUserId");
        propertyNames.Should().NotContain("Id");
        propertyNames.Should().NotContain("Email");
    }

    [Theory]
    [InlineData("ValidPassword123!", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void DeleteAccountValidator_ValidatesPassword(string? password, bool isValid)
    {
        var validator = new DeleteAccountCommandValidator();
        var result = validator.Validate(new DeleteAccountCommand { Password = password! });
        result.IsValid.Should().Be(isValid);
    }

    // =========================================================================
    // 4. Controller HTTP Semantics Tests
    // =========================================================================

    [Fact]
    public async Task AuthController_UpdateProfile_Success_Returns200WithDto()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        var profileDto = new UserProfileDto(Guid.NewGuid(), "a@b.com", "New Name", DateTime.UtcNow, 0, 1, 0, 0);
        mediatorMock.Setup(m => m.Send(It.IsAny<UpdateProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileDto);

        var result = await controller.UpdateProfile(new UpdateProfileCommand { DisplayName = "New Name" });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(profileDto);
    }

    [Fact]
    public async Task AuthController_UpdateProfile_ValidationFailure_Returns400()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<UpdateProfileCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("DisplayName", "Required") }));

        var result = await controller.UpdateProfile(new UpdateProfileCommand { DisplayName = "" });

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task AuthController_UpdateProfile_UserNotFound_Returns404()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<UpdateProfileCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UserNotFoundException(Guid.NewGuid()));

        var result = await controller.UpdateProfile(new UpdateProfileCommand { DisplayName = "Name" });

        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task AuthController_UpdateProfile_Unauthenticated_Returns401()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<UpdateProfileCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await controller.UpdateProfile(new UpdateProfileCommand { DisplayName = "Name" });

        var unauthResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task AuthController_UpdateProfile_UnexpectedException_Returns500()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<UpdateProfileCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection error"));

        var result = await controller.UpdateProfile(new UpdateProfileCommand { DisplayName = "Name" });

        var errorResult = result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task AuthController_DeleteAccount_Success_Returns200WithMessage()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<DeleteAccountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await controller.DeleteAccount(new DeleteAccountCommand { Password = "Password123!" });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task AuthController_DeleteAccount_InvalidCredentials_Returns400()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<DeleteAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialsException("Password is incorrect."));

        var result = await controller.DeleteAccount(new DeleteAccountCommand { Password = "Wrong!" });

        var badResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task AuthController_DeleteAccount_ValidationFailure_Returns400()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<DeleteAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("Password", "Required") }));

        var result = await controller.DeleteAccount(new DeleteAccountCommand { Password = "" });

        var badResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task AuthController_DeleteAccount_Unauthenticated_Returns401()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<DeleteAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await controller.DeleteAccount(new DeleteAccountCommand { Password = "Pass" });

        var unauthResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task AuthController_DeleteAccount_UserNotFound_Returns404()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<DeleteAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UserNotFoundException(Guid.NewGuid()));

        var result = await controller.DeleteAccount(new DeleteAccountCommand { Password = "Pass" });

        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task AuthController_DeleteAccount_UnexpectedException_Returns500()
    {
        var mediatorMock = new Mock<IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        mediatorMock.Setup(m => m.Send(It.IsAny<DeleteAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fatal DB error"));

        var result = await controller.DeleteAccount(new DeleteAccountCommand { Password = "Pass" });

        var errorResult = result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);
    }

    // =========================================================================
    // 5. JWT & Post-Deletion Handlers Behavior Tests
    // =========================================================================

    [Fact]
    public void JwtProvider_GeneratesValidToken_WithRequiredClaimsAnd7DayExpiry()
    {
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["Jwt:Secret"]).Returns("super_secret_jwt_key_that_is_long_enough_for_hmac256!");
        configMock.Setup(c => c["Jwt:Issuer"]).Returns("MOT.Api");
        configMock.Setup(c => c["Jwt:Audience"]).Returns("MOT.Frontend");

        var jwtProvider = new JwtProvider(configMock.Object);
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "jwtuser@example.com",
            DisplayName = "JWT User"
        };

        var tokenString = jwtProvider.GenerateToken(user);
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        token.Issuer.Should().Be("MOT.Api");
        token.Audiences.Should().Contain("MOT.Frontend");
        token.Claims.First(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub).Value.Should().Be(userId.ToString());
        token.Claims.First(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email).Value.Should().Be("jwtuser@example.com");
        token.Claims.First(c => c.Type == "DisplayName").Value.Should().Be("JWT User");
        token.ValidTo.Should().BeAfter(DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task PostDeletion_AllUserSpecificHandlers_ThrowUserNotFoundException()
    {
        // When a user has been deleted from the database, handlers looking up the user entity
        // throw UserNotFoundException even if the client holds an unexpired JWT.
        var deletedUserId = Guid.NewGuid();
        var users = new List<User>(); // Empty database (user deleted)

        var dbContextMock = new Mock<IAppDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);
        dbContextMock.Setup(c => c.StudySessions).Returns(MockDbSetHelper.CreateMockDbSet(new List<StudySession>()).Object);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(deletedUserId.ToString());

        var authRepoMock = new Mock<IAuthRepository>();
        authRepoMock.Setup(r => r.GetUserByIdAsync(deletedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // 1. GetCurrentUserQueryHandler throws UserNotFoundException
        var getProfileHandler = new MOT.Application.Users.Queries.GetCurrentUserQueryHandler(
            authRepoMock.Object, currentUserServiceMock.Object, dbContextMock.Object);
        var actProfile = async () => await getProfileHandler.Handle(new MOT.Application.Users.Queries.GetCurrentUserQuery(), CancellationToken.None);
        await actProfile.Should().ThrowAsync<UserNotFoundException>();

        // 2. UpdateProfileCommandHandler throws UserNotFoundException
        var updateProfileHandler = new UpdateProfileCommandHandler(dbContextMock.Object, currentUserServiceMock.Object);
        var actUpdate = async () => await updateProfileHandler.Handle(new UpdateProfileCommand { DisplayName = "New Name" }, CancellationToken.None);
        await actUpdate.Should().ThrowAsync<UserNotFoundException>();

        // 3. ChangePasswordCommandHandler throws UserNotFoundException
        var changePasswordHandler = new ChangePasswordCommandHandler(dbContextMock.Object, currentUserServiceMock.Object, _passwordHasher);
        var actPassword = async () => await changePasswordHandler.Handle(new ChangePasswordCommand { CurrentPassword = "old", NewPassword = "new" }, CancellationToken.None);
        await actPassword.Should().ThrowAsync<UserNotFoundException>();

        // 4. DeleteAccountCommandHandler throws UserNotFoundException
        var deleteAccountHandler = new DeleteAccountCommandHandler(dbContextMock.Object, currentUserServiceMock.Object, _passwordHasher);
        var actDelete = async () => await deleteAccountHandler.Handle(new DeleteAccountCommand { Password = "pass" }, CancellationToken.None);
        await actDelete.Should().ThrowAsync<UserNotFoundException>();
    }
}

