using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;

namespace MOT.Application.Users.Commands;

public class SendPasswordResetOtpCommand : IRequest<bool>
{
    public string Email { get; set; } = string.Empty;
}

public class SendPasswordResetOtpCommandHandler : IRequestHandler<SendPasswordResetOtpCommand, bool>
{
    private readonly IAppDbContext _context;
    private readonly IEmailService _emailService;

    public SendPasswordResetOtpCommandHandler(IAppDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<bool> Handle(SendPasswordResetOtpCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return false;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (user == null)
        {
            // For security, don't reveal if user exists, but don't fail either
            return true;
        }

        // Generate 6 digit numeric code
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var codeHash = HashOtp(normalizedEmail, code);

        // Invalidate older unused OTPs for this email
        var existingOtps = await _context.PasswordResetOtps
            .Where(o => o.Email == normalizedEmail && !o.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var otp in existingOtps)
        {
            otp.IsUsed = true;
        }

        var newOtp = new PasswordResetOtp
        {
            Email = normalizedEmail,
            OtpHash = codeHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.PasswordResetOtps.Add(newOtp);
        await _context.SaveChangesAsync(cancellationToken);

        // Send email with OTP
        await _emailService.SendPasswordResetOtpAsync(user.Email, code, cancellationToken);

        return true;
    }

    public static string HashOtp(string email, string code)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{email}:{code}:MOT_OTP_SALT_2026");
        return Convert.ToBase64String(sha.ComputeHash(bytes));
    }
}
