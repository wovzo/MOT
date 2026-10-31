using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Users.Commands;
using MOT.Domain.Entities;
using MOT.Domain.Interfaces;
using MOT.Infrastructure.Authentication;
using MOT.Tests.Common;
using Xunit;

namespace MOT.Tests.Authentication;

public class PasswordResetOtpTests
{
    private readonly Mock<IAppDbContext> _dbContextMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly IPasswordHasher _passwordHasher = new PasswordHasher();

    [Fact]
    public async Task SendOtp_WhenUserDoesNotExist_ReturnsTrueWithoutSendingEmail()
    {
        var users = new List<User>();
        _dbContextMock.Setup(db => db.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);

        var handler = new SendPasswordResetOtpCommandHandler(_dbContextMock.Object, _emailServiceMock.Object);
        var result = await handler.Handle(new SendPasswordResetOtpCommand { Email = "notfound@example.com" }, CancellationToken.None);

        result.Should().BeTrue();
        _emailServiceMock.Verify(e => e.SendPasswordResetOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendOtp_WhenUserExists_CreatesOtpAndDispatchesEmail()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hash" };
        var users = new List<User> { user };
        var otps = new List<PasswordResetOtp>();

        _dbContextMock.Setup(db => db.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);
        _dbContextMock.Setup(db => db.PasswordResetOtps).Returns(MockDbSetHelper.CreateMockDbSet(otps).Object);

        var handler = new SendPasswordResetOtpCommandHandler(_dbContextMock.Object, _emailServiceMock.Object);
        var result = await handler.Handle(new SendPasswordResetOtpCommand { Email = "user@example.com" }, CancellationToken.None);

        result.Should().BeTrue();
        otps.Should().HaveCount(1);
        otps[0].Email.Should().Be("user@example.com");
        otps[0].IsUsed.Should().BeFalse();
        otps[0].ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        _emailServiceMock.Verify(e => e.SendPasswordResetOtpAsync("user@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordWithOtp_WhenOtpIsInvalid_ThrowsInvalidOperationException()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "old_hash" };
        var users = new List<User> { user };
        var otps = new List<PasswordResetOtp>
        {
            new()
            {
                Email = "user@example.com",
                OtpHash = SendPasswordResetOtpCommandHandler.HashOtp("user@example.com", "123456"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        _dbContextMock.Setup(db => db.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);
        _dbContextMock.Setup(db => db.PasswordResetOtps).Returns(MockDbSetHelper.CreateMockDbSet(otps).Object);

        var handler = new ResetPasswordWithOtpCommandHandler(_dbContextMock.Object, _passwordHasher);
        
        var act = async () => await handler.Handle(new ResetPasswordWithOtpCommand
        {
            Email = "user@example.com",
            Otp = "999999", // wrong OTP
            NewPassword = "NewPassword123!"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid or expired verification code.");
    }

    [Fact]
    public async Task ResetPasswordWithOtp_WhenOtpIsValid_UpdatesPasswordAndMarksUsed()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "old_hash" };
        var users = new List<User> { user };
        var activeOtp = new PasswordResetOtp
        {
            Email = "user@example.com",
            OtpHash = SendPasswordResetOtpCommandHandler.HashOtp("user@example.com", "654321"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        var otps = new List<PasswordResetOtp> { activeOtp };

        _dbContextMock.Setup(db => db.Users).Returns(MockDbSetHelper.CreateMockDbSet(users).Object);
        _dbContextMock.Setup(db => db.PasswordResetOtps).Returns(MockDbSetHelper.CreateMockDbSet(otps).Object);

        var handler = new ResetPasswordWithOtpCommandHandler(_dbContextMock.Object, _passwordHasher);
        
        var result = await handler.Handle(new ResetPasswordWithOtpCommand
        {
            Email = "user@example.com",
            Otp = "654321",
            NewPassword = "NewSecretPassword123!"
        }, CancellationToken.None);

        result.Should().BeTrue();
        activeOtp.IsUsed.Should().BeTrue();
        user.PasswordHash.Should().NotBe("old_hash");
        _passwordHasher.VerifyPassword("NewSecretPassword123!", user.PasswordHash).Should().BeTrue();
    }
}
