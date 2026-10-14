using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.Subjects.Commands;

public class DeleteSubjectCommand : IRequest<Unit>
{
    public Guid Id { get; set; }

    public DeleteSubjectCommand(Guid id)
    {
        Id = id;
    }
}

public class DeleteSubjectCommandHandler : IRequestHandler<DeleteSubjectCommand, Unit>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteSubjectCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(DeleteSubjectCommand request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedAccessException();

        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.UserId == userId, cancellationToken);

        if (subject == null)
        {
            throw new SubjectNotFoundException(request.Id);
        }

        _context.Subjects.Remove(subject);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
