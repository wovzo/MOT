using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.StudySessions;

public class ResumeStudySessionCommand : IRequest<StudySessionDto>
{
    public Guid SessionId { get; set; }
}

public class ResumeStudySessionCommandHandler : IRequestHandler<ResumeStudySessionCommand, StudySessionDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ResumeStudySessionCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<StudySessionDto> Handle(ResumeStudySessionCommand request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedAccessException();

        using (await UserConcurrencyLock.LockAsync(userId, cancellationToken))
        {
            var session = await _context.StudySessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == userId, cancellationToken);

            if (session == null)
                throw new StudySessionNotFoundException(request.SessionId);

            if (session.IsCompleted)
                throw new InvalidStudySessionStateException("Cannot resume a study session that has already ended.");

            var now = DateTime.UtcNow;

            // Idempotent: only transition to running if currently paused
            if (session.IsPaused)
            {
                session.IsPaused = false;
                session.LastResumedAt = now;
                session.LastPausedAt = null;

                await _context.SaveChangesAsync(cancellationToken);
            }

            return session.ToDto(now);
        }
    }
}
