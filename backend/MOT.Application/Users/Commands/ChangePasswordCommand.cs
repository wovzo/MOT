using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Interfaces;

namespace MOT.Application.Users.Commands;

public class ChangePasswordCommand : IRequest<bool>
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;

    public ChangePasswordCommandHandler(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
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

        // Verify current password against stored PBKDF2 hash
        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidCredentialsException("Current password is incorrect.");
        }

        // Hash and persist new password
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
