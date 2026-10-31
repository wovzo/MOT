using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Interfaces;

namespace MOT.Application.Users.Commands;

public class ResetPasswordWithOtpCommand : IRequest<bool>
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordWithOtpCommandHandler : IRequestHandler<ResetPasswordWithOtpCommand, bool>
{
    private readonly IAppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordWithOtpCommandHandler(IAppDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(ResetPasswordWithOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var cleanOtp = request.Otp?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(cleanOtp) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new ArgumentException("Email, OTP, and new password are required.");
        }

        if (request.NewPassword.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(Guid.Empty);
        }

        var expectedHash = SendPasswordResetOtpCommandHandler.HashOtp(normalizedEmail, cleanOtp);

        var activeOtp = await _context.PasswordResetOtps
            .Where(o => o.Email == normalizedEmail && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeOtp == null || activeOtp.OtpHash != expectedHash)
        {
            throw new InvalidOperationException("Invalid or expired verification code.");
        }

        // Mark OTP as used
        activeOtp.IsUsed = true;

        // Hash and update password
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
