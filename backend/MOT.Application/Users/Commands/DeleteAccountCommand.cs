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

public class DeleteAccountCommand : IRequest<bool>
{
    public string Password { get; set; } = string.Empty;
}

public class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, bool>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;

    public DeleteAccountCommandHandler(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        // Verify password against stored PBKDF2 hash before any destructive action
        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException("Password is incorrect.");
        }

        // Clean up any active classroom participants belonging to this user
        var participants = await _context.RoomParticipants
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        if (participants.Count > 0)
        {
            _context.RoomParticipants.RemoveRange(participants);
        }

        // Remove user entity (Tasks, Habits, HabitCompletions, StudySessions cascade via DB foreign keys)
        _context.Users.Remove(user);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
