using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace MOT.Application.Users.Commands;

[Obsolete("Unauthenticated password reset by email is disabled. Use authenticated ChangePasswordCommand or a secure token recovery flow.")]
public class ResetPasswordCommand : IRequest<bool>
{
    public string Email { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

[Obsolete("Unauthenticated password reset by email is disabled.")]
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    public Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // For security, unauthenticated password reset by email is disabled.
        // It never queries or modifies user accounts.
        return Task.FromResult(false);
    }
}
