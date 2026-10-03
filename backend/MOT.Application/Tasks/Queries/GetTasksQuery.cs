using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.Tasks.Queries
{
    public class GetTasksQuery : IRequest<List<DailyTaskDto>>
    {
    }

    public record DailyTaskDto(Guid Id, string Title, string? Description, bool IsCompleted, DateTime CreatedAt);

    public class GetTasksQueryHandler : IRequestHandler<GetTasksQuery, List<DailyTaskDto>>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetTasksQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<DailyTaskDto>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            return await _context.Tasks
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new DailyTaskDto(t.Id, t.Title, t.Description, t.IsCompleted, t.CreatedAt))
                .ToListAsync(cancellationToken);
        }
    }
}
