using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;
using MOT.Domain.Interfaces;
using MOT.Infrastructure.Persistence;
// Typically you'd use BCrypt.Net-Next here, but for MVP scaffolding we simulate it.

namespace MOT.Infrastructure.Authentication;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;

    public AuthRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<User> CreateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        // MVP Simulation: Hash the password. In production, use BCrypt.HashPassword.
        user.PasswordHash = $"hashed_{user.PasswordHash}"; 
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> IsEmailUniqueAsync(string email, CancellationToken cancellationToken = default)
    {
        return !await _context.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }
}
