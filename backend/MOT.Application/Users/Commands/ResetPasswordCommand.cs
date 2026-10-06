using MediatR;
using System.Threading;
using System.Threading.Tasks;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;

namespace MOT.Application.Users.Commands;

public class ResetPasswordCommand : IRequest<bool>
{
    public string Email { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly IAppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordCommandHandler(IAppDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user == null)
        {
            return false; // User not found
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
