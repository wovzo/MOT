using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.Tasks.Commands
{
    public class DeleteTaskCommand : IRequest<Unit>
    {
        public Guid Id { get; set; }

        public DeleteTaskCommand() { }

        public DeleteTaskCommand(Guid id)
        {
            Id = id;
        }
    }

    public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Unit>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public DeleteTaskCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<Unit> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
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

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
