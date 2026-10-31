using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MOT.Application.Common.Interfaces;

namespace MOT.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPasswordResetOtpAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Smtp:Host"] ?? Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.stackmail.com";
        var portStr = _configuration["Smtp:Port"] ?? Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
        int.TryParse(portStr, out var port);
        if (port == 0) port = 587;

        var username = _configuration["Smtp:Username"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? "noreply@wovzo.com";
        var password = _configuration["Smtp:Password"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? string.Empty;
        var senderEmail = _configuration["Smtp:SenderEmail"] ?? Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL") ?? username;
        var senderName = _configuration["Smtp:SenderName"] ?? Environment.GetEnvironmentVariable("SMTP_SENDER_NAME") ?? "Mind On Track";

        var enableSsl = bool.Parse(_configuration["Smtp:EnableSsl"] ?? Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL") ?? "true");

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("SMTP Password is not configured. Email will not be dispatched to {Email}. OTP code: {Otp}", toEmail, otpCode);
            // In dev/test when password isn't set yet, don't crash, log it for verification
            return;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = $"{otpCode} is your Mind On Track verification code",
                IsBodyHtml = true,
                Body = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0F0C20; color: #FFFFFF; padding: 40px 20px; text-align: center;"">
  <div style=""max-width: 480px; margin: 0 auto; background-color: #1A1635; border-radius: 16px; padding: 32px; border: 1px solid rgba(255, 255, 255, 0.1);"">
    <h1 style=""color: #6C5CE7; font-size: 26px; margin-bottom: 8px;"">Mind On Track</h1>
    <p style=""color: #A0A0B2; font-size: 15px; margin-bottom: 24px;"">Password Reset Request</p>
    <p style=""color: #E2E8F0; font-size: 14px; line-height: 1.6;"">You requested to reset your password. Use the following 6-digit verification code to complete your password reset:</p>
    <div style=""background-color: #26214D; border: 2px dashed #6C5CE7; border-radius: 12px; padding: 18px; margin: 28px 0; font-size: 32px; letter-spacing: 8px; font-weight: bold; color: #00D9C0;"">
      {otpCode}
    </div>
    <p style=""color: #A0A0B2; font-size: 13px;"">This verification code will expire in <strong>15 minutes</strong>.</p>
    <p style=""color: #718096; font-size: 12px; margin-top: 24px;"">If you didn't request a password reset, you can safely ignore this email.</p>
  </div>
</div>"
            };

            message.To.Add(new MailAddress(toEmail));

            _logger.LogInformation("Sending password reset email to {Email} via SMTP {Host}:{Port}", toEmail, host, port);
            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Password reset email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            throw new InvalidOperationException("Failed to send verification email. Please check server email settings.", ex);
        }
    }
}
