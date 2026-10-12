using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Tasks.Queries;

namespace MOT.Application.Tasks.Commands
{
    public class UpdateTaskCommand : IRequest<DailyTaskDto>
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool? IsCompleted { get; set; }
    }

    public class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, DailyTaskDto>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public UpdateTaskCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<DailyTaskDto> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
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

            task.Title = request.Title;
            task.Description = request.Description;

            if (request.IsCompleted.HasValue)
            {
                task.IsCompleted = request.IsCompleted.Value;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new DailyTaskDto(task.Id, task.Title, task.Description, task.IsCompleted, task.CreatedAt);
        }
    }
}
