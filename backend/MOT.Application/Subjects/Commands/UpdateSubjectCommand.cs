using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Subjects.DTOs;

namespace MOT.Application.Subjects.Commands;

public class UpdateSubjectCommand : IRequest<SubjectDto>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ColorHex { get; set; }
}

public class UpdateSubjectCommandHandler : IRequestHandler<UpdateSubjectCommand, SubjectDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateSubjectCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<SubjectDto> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
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

        var normalizedName = request.Name.Trim();

        var duplicate = await _context.Subjects
            .AnyAsync(s => s.UserId == userId && s.Id != request.Id && s.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (duplicate)
        {
            throw new DuplicateSubjectNameException(normalizedName);
        }

        subject.Name = normalizedName;
        subject.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.ColorHex))
        {
            subject.ColorHex = request.ColorHex.Trim();
        }
        subject.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

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
