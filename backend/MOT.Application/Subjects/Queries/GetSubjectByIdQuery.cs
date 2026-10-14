using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Subjects.DTOs;

namespace MOT.Application.Subjects.Queries;

public class GetSubjectByIdQuery : IRequest<SubjectDto>
{
    public Guid Id { get; set; }

    public GetSubjectByIdQuery(Guid id)
    {
        Id = id;
    }
}

public class GetSubjectByIdQueryHandler : IRequestHandler<GetSubjectByIdQuery, SubjectDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSubjectByIdQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<SubjectDto> Handle(GetSubjectByIdQuery request, CancellationToken cancellationToken)
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

        return new SubjectDto
        {
            Id = subject.Id,
            Name = subject.Name,
            Description = subject.Description,
            ColorHex = subject.ColorHex,
            CreatedAt = subject.CreatedAt,
            UpdatedAt = subject.UpdatedAt
        };
    }
}
