using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Subjects.DTOs;
using MOT.Domain.Entities;

namespace MOT.Application.Subjects.Commands;

public class CreateSubjectCommand : IRequest<SubjectDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ColorHex { get; set; }
}

public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, SubjectDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateSubjectCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<SubjectDto> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedAccessException();

        var normalizedName = request.Name.Trim();

        var exists = await _context.Subjects
            .AnyAsync(s => s.UserId == userId && s.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new DuplicateSubjectNameException(normalizedName);
        }

        var subject = new Subject
        {
            UserId = userId,
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? "#6C5CE7" : request.ColorHex.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Subjects.Add(subject);
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
