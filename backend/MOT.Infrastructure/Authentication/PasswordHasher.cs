using Microsoft.AspNetCore.Identity;
using MOT.Domain.Entities;
using MOT.Domain.Interfaces;

namespace MOT.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(null!, password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            var result = _hasher.VerifyHashedPassword(null!, passwordHash, password);
            return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch
        {
            // Invalid format (e.g. legacy plain text or corrupted prefix) fails verification safely
            return false;
        }
    }
}
