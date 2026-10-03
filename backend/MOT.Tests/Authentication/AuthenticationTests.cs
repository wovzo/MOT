using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using MOT.Api.Controllers;
using MOT.Application.Common.Exceptions;
using MOT.Application.DTOs;
using MOT.Application.Handlers;
using MOT.Application.Validators;
using MOT.Domain.Entities;
using MOT.Domain.Interfaces;
using MOT.Infrastructure.Authentication;
using Xunit;

namespace MOT.Tests.Authentication;

public class AuthenticationTests
{
    private readonly Mock<IAuthRepository> _authRepoMock = new();
    private readonly Mock<IJwtProvider> _jwtProviderMock = new();
    private readonly PasswordHasher _passwordHasher = new();

    // 1. Valid registration succeeds
    [Fact]
    public async Task Register_WithValidRequest_Succeeds()
    {
        var request = new RegisterRequest("test@example.com", "Password123!", "Test User");
        _authRepoMock.Setup(r => r.IsEmailUniqueAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _authRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);
        _jwtProviderMock.Setup(j => j.GenerateToken(It.IsAny<User>()))
            .Returns("valid_mock_jwt_token");

        var handler = new RegisterUserHandler(_authRepoMock.Object, _jwtProviderMock.Object, _passwordHasher);
        var response = await handler.Handle(request, CancellationToken.None);

        response.Should().NotBeNull();
        response.Token.Should().Be("valid_mock_jwt_token");
        response.Email.Should().Be("test@example.com");
        response.DisplayName.Should().Be("Test User");
        response.UserId.Should().NotBeEmpty();
    }

    // 2. Password is stored as a real hash, not plaintext
    [Fact]
    public async Task Register_StoresRealPasswordHash_NotPlaintextOrPrefix()
    {
        var password = "SecurePassword@123";
        var request = new RegisterRequest("hash_check@example.com", password, "Hash Tester");
        User? capturedUser = null;

        _authRepoMock.Setup(r => r.IsEmailUniqueAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _authRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u)
            .ReturnsAsync((User u, CancellationToken _) => u);
        _jwtProviderMock.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns("token");

        var handler = new RegisterUserHandler(_authRepoMock.Object, _jwtProviderMock.Object, _passwordHasher);
        await handler.Handle(request, CancellationToken.None);

        capturedUser.Should().NotBeNull();
        capturedUser!.PasswordHash.Should().NotBeNullOrWhiteSpace();
        capturedUser.PasswordHash.Should().NotBe(password);
        capturedUser.PasswordHash.Should().NotStartWith("hashed_");
        _passwordHasher.VerifyPassword(password, capturedUser.PasswordHash).Should().BeTrue();
        _passwordHasher.VerifyPassword("WrongPassword!", capturedUser.PasswordHash).Should().BeFalse();
    }

    // 3. Password is not returned in response
    [Fact]
    public async Task Register_ResponseDoesNotContainPasswordOrHash()
    {
        var request = new RegisterRequest("response_check@example.com", "Password123!", "No Password In Response");
        _authRepoMock.Setup(r => r.IsEmailUniqueAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _authRepoMock.Setup(r => r.CreateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);
        _jwtProviderMock.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns("token");

        var handler = new RegisterUserHandler(_authRepoMock.Object, _jwtProviderMock.Object, _passwordHasher);
        var response = await handler.Handle(request, CancellationToken.None);

        // Verify AuthResponse type structure contains only safe fields
        typeof(AuthResponse).GetProperty("Password").Should().BeNull();
        typeof(AuthResponse).GetProperty("PasswordHash").Should().BeNull();
    }

    // 4. Duplicate email is rejected
    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsDuplicateEmailException()
    {
        var request = new RegisterRequest("duplicate@example.com", "Password123!", "Dupe Tester");
        _authRepoMock.Setup(r => r.IsEmailUniqueAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new RegisterUserHandler(_authRepoMock.Object, _jwtProviderMock.Object, _passwordHasher);
        Func<Task> act = async () => await handler.Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateEmailException>()
            .WithMessage("*duplicate@example.com*");
        _authRepoMock.Verify(r => r.CreateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 5. Invalid email is rejected by validator
    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@missinguser.com")]
    public void RegisterValidator_WithInvalidEmail_FailsValidation(string invalidEmail)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(invalidEmail, "Password123!", "Test User");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    // 6. Missing password is rejected by validator
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RegisterValidator_WithMissingPassword_FailsValidation(string? missingPassword)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("valid@example.com", missingPassword!, "Test User");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    // 7. Password below minimum length is rejected by validator
    [Theory]
    [InlineData("12345")]
    [InlineData("a")]
    [InlineData("abc")]
    public void RegisterValidator_WithShortPassword_FailsValidation(string shortPassword)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("valid@example.com", shortPassword, "Test User");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    // 8. Valid credentials succeed
    [Fact]
    public async Task Login_WithValidCredentials_SucceedsAndReturnsToken()
    {
        var email = "login_test@example.com";
        var password = "CorrectPassword123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Logged In User",
            PasswordHash = _passwordHasher.HashPassword(password)
        };

        _authRepoMock.Setup(r => r.GetUserByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtProviderMock.Setup(j => j.GenerateToken(user)).Returns("jwt_success_token");

        var handler = new LoginUserHandler(_authRepoMock.Object, _jwtProviderMock.Object, _passwordHasher);
        var response = await handler.Handle(new LoginRequest(email, password), CancellationToken.None);

        response.Should().NotBeNull();
        response.Token.Should().Be("jwt_success_token");
        response.Email.Should().Be(email);
        response.UserId.Should().Be(user.Id);
    }

    // 9. Incorrect password is rejected
    [Fact]
    public async Task Login_WithIncorrectPassword_ThrowsInvalidCredentialsException()
    {
        var email = "login_test@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "User",
            PasswordHash = _passwordHasher.HashPassword("CorrectPassword123!")
        };

        _authRepoMock.Setup(r => r.GetUserByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new LoginUserHandler(_authRepoMock.Object, _jwtProviderMock.Object, _passwordHasher);
        Func<Task> act = async () => await handler.Handle(new LoginRequest(email, "WrongPassword"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _jwtProviderMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    // 10. Unknown email is rejected without account enumeration detail
    [Fact]
    public async Task Login_WithUnknownEmail_ThrowsInvalidCredentialsExceptionWithoutEnumeration()
    {
        _authRepoMock.Setup(r => r.GetUserByEmailAsync("nonexistent@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new LoginUserHandler(_authRepoMock.Object, _jwtProviderMock.Object, _passwordHasher);
        Func<Task> act = async () => await handler.Handle(new LoginRequest("nonexistent@example.com", "SomePassword"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<InvalidCredentialsException>();
        exception.WithMessage("Invalid email or password.");
        _jwtProviderMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    // 11. JWT is returned only after successful authentication
    [Fact]
    public async Task Login_GeneratesTokenOnlyAfterSuccessfulVerification()
    {
        var email = "token_only@example.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Token Only",
            PasswordHash = _passwordHasher.HashPassword("ValidPassword!")
        };

        _authRepoMock.Setup(r => r.GetUserByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtProviderMock.Setup(j => j.GenerateToken(user)).Returns("proper_token");

        var handler = new LoginUserHandler(_authRepoMock.Object, _jwtProviderMock.Object, _passwordHasher);
        var result = await handler.Handle(new LoginRequest(email, "ValidPassword!"), CancellationToken.None);

        result.Token.Should().Be("proper_token");
        _jwtProviderMock.Verify(j => j.GenerateToken(user), Times.Once);
    }

    // 12. JWT configuration cannot silently fall back to a hardcoded secret
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void JwtProvider_WhenSecretIsMissing_ThrowsInvalidOperationException(string? emptySecret)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = emptySecret
            })
            .Build();

        var jwtProvider = new JwtProvider(config);
        var user = new User { Id = Guid.NewGuid(), Email = "user@test.com" };

        Action act = () => jwtProvider.GenerateToken(user);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT Secret is not configured*");
    }

    // 13. Authentication exceptions do not become generic HTTP 400 responses
    [Fact]
    public async Task AuthController_ExceptionsMapToAppropriateHttpSemantics()
    {
        var mediatorMock = new Mock<MediatR.IMediator>();
        var loggerMock = new Mock<ILogger<AuthController>>();
        var controller = new AuthController(mediatorMock.Object, loggerMock.Object);

        // ValidationException -> 400 Bad Request
        mediatorMock.Setup(m => m.Send(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[] { new ValidationFailure("Email", "Email invalid") }));
        var valResult = await controller.Register(new RegisterRequest("bad", "123", "User"));
        valResult.Should().BeOfType<BadRequestObjectResult>();

        // DuplicateEmailException -> 409 Conflict
        mediatorMock.Setup(m => m.Send(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateEmailException("existing@example.com"));
        var conflictResult = await controller.Register(new RegisterRequest("existing@example.com", "Password123!", "User"));
        conflictResult.Should().BeOfType<ConflictObjectResult>();

        // InvalidCredentialsException -> 401 Unauthorized
        mediatorMock.Setup(m => m.Send(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialsException());
        var unauthResult = await controller.Login(new LoginRequest("user@test.com", "wrong"));
        unauthResult.Should().BeOfType<UnauthorizedObjectResult>();

        // Unexpected general exception (e.g. database down) -> 500 Internal Server Error (NOT 400)
        mediatorMock.Setup(m => m.Send(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection timeout."));
        var errorResult = await controller.Register(new RegisterRequest("user@test.com", "Password123!", "User"));
        var statusResult = errorResult.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }
}
