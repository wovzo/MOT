using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.Tasks.Queries
{
    public class GetTaskByIdQuery : IRequest<DailyTaskDto>
    {
        public Guid Id { get; set; }

        public GetTaskByIdQuery() { }

        public GetTaskByIdQuery(Guid id)
        {
            Id = id;
        }
    }

    public class GetTaskByIdQueryHandler : IRequestHandler<GetTaskByIdQuery, DailyTaskDto>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetTaskByIdQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<DailyTaskDto> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
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

            return new DailyTaskDto(task.Id, task.Title, task.Description, task.IsCompleted, task.CreatedAt);
        }
    }
}
