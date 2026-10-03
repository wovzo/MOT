using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;

namespace MOT.Application.Tasks.Commands
{
    public class CreateTaskCommand : IRequest<Guid>
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Guid>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public CreateTaskCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<Guid> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var task = new DailyTask
            {
                UserId = userId,
                Title = request.Title,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                IsCompleted = false
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync(cancellationToken);

            return task.Id;
        }
    }
}
