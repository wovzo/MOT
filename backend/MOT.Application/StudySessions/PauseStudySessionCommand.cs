using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.StudySessions;

public class PauseStudySessionCommand : IRequest<StudySessionDto>
{
    public Guid SessionId { get; set; }
}

public class PauseStudySessionCommandHandler : IRequestHandler<PauseStudySessionCommand, StudySessionDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public PauseStudySessionCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<StudySessionDto> Handle(PauseStudySessionCommand request, CancellationToken cancellationToken)
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
                throw new InvalidStudySessionStateException("Cannot pause a study session that has already ended.");

            var now = DateTime.UtcNow;

            // Idempotent: only transition and accumulate if currently running
            if (!session.IsPaused)
            {
                var resumedAt = session.LastResumedAt ?? session.StartTime;
                var activeElapsed = Math.Max(0, (int)(now - resumedAt).TotalSeconds);

                session.AccumulatedSeconds += activeElapsed;
                session.IsPaused = true;
                session.LastPausedAt = now;
                session.LastResumedAt = null;
                session.DurationMinutes = Math.Max(0, session.AccumulatedSeconds / 60);

                await _context.SaveChangesAsync(cancellationToken);
            }

            return session.ToDto(now);
        }
    }
}
