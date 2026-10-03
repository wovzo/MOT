using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.Tasks.Commands
{
    public class ToggleTaskCommand : IRequest<bool>
    {
        public Guid Id { get; set; }

        public ToggleTaskCommand() { }

        public ToggleTaskCommand(Guid id)
        {
            Id = id;
        }
    }

    public class ToggleTaskCommandHandler : IRequestHandler<ToggleTaskCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ToggleTaskCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(ToggleTaskCommand request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == request.Id && t.UserId == userId, cancellationToken);

            if (task == null)
            {
                throw new TaskNotFoundException(request.Id);
            }

            task.IsCompleted = !task.IsCompleted;
            await _context.SaveChangesAsync(cancellationToken);

            return task.IsCompleted;
        }
    }
}
