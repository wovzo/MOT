using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Gamification;
using MOT.Domain.Entities;
using MOT.Domain.Gamification;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MOT.Application.StudySessions
{
    public class EndStudySessionCommand : IRequest<StudySessionDto>
    {
        public Guid SessionId { get; set; }
    }

    public class EndStudySessionCommandHandler : IRequestHandler<EndStudySessionCommand, StudySessionDto>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IGamificationService _gamificationService;
        private readonly IAchievementService _achievementService;

        public EndStudySessionCommandHandler(
            IAppDbContext context,
            ICurrentUserService currentUserService,
            IGamificationService gamificationService,
            IAchievementService achievementService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _gamificationService = gamificationService;
            _achievementService = achievementService;
        }

        public async Task<StudySessionDto> Handle(EndStudySessionCommand request, CancellationToken cancellationToken)
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
                    
                if (!session.IsCompleted)
                {
                    var now = DateTime.UtcNow;

                    if (!session.IsPaused)
                    {
                        var resumedAt = session.LastResumedAt ?? session.StartTime;
                        var activeElapsed = Math.Max(0, (int)(now - resumedAt).TotalSeconds);
                        session.AccumulatedSeconds += activeElapsed;
                    }

                    session.EndTime = now;
                    session.DurationMinutes = Math.Max(0, session.AccumulatedSeconds / 60);
                    session.IsCompleted = true;
                    session.IsPaused = false;
                    session.LastPausedAt = null;
                    session.ActiveUserId = null;

                    await _gamificationService.AwardStudySessionXpAsync(userId, session, cancellationToken);
                    await _achievementService.EvaluateStudySessionAchievementsAsync(userId, session, cancellationToken);
                    
                    try
                    {
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateException ex) when (_gamificationService.IsXpDuplicateCollision(ex) || _achievementService.IsAchievementDuplicateCollision(ex))
                    {
                        _gamificationService.HandleDuplicateCollision(userId, XpSourceTypes.StudySession, session.Id.ToString());
                        if (_context is DbContext dbContext)
                        {
                            var sessionEntry = dbContext.ChangeTracker.Entries<StudySession>().FirstOrDefault(e => e.Entity.Id == session.Id);
                            sessionEntry?.Reload();
                        }
                    }
                }

                return session.ToDto();
            }
        }
    }
}
