using MediatR;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;

namespace MOT.Application.Tasks.Commands;

public record CreateTaskCommand(Guid UserId, string Title, string? Description) : IRequest<Guid>;

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Guid>
{
    private readonly IAppDbContext _context;

    public CreateTaskCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = new DailyTask
        {
            UserId = request.UserId,
            Title = request.Title,
            Description = request.Description
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        return task.Id;
    }
}
