using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;

namespace MOT.Application.Tasks.Queries;

public record GetTasksQuery(Guid UserId) : IRequest<List<DailyTaskDto>>;

public record DailyTaskDto(Guid Id, string Title, string? Description, bool IsCompleted, DateTime CreatedAt);

public class GetTasksQueryHandler : IRequestHandler<GetTasksQuery, List<DailyTaskDto>>
{
    private readonly IAppDbContext _context;

    public GetTasksQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<List<DailyTaskDto>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
    {
        return await _context.Tasks
            .Where(t => t.UserId == request.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new DailyTaskDto(t.Id, t.Title, t.Description, t.IsCompleted, t.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
