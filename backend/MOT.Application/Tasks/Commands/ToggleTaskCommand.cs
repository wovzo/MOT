using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.Tasks.Commands;

public record ToggleTaskCommand(Guid Id, Guid UserId) : IRequest<bool>;

public class ToggleTaskCommandHandler : IRequestHandler<ToggleTaskCommand, bool>
{
    private readonly IAppDbContext _context;

    public ToggleTaskCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ToggleTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.UserId == request.UserId, cancellationToken);

        if (task == null)
            return false;

        task.IsCompleted = !task.IsCompleted;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
