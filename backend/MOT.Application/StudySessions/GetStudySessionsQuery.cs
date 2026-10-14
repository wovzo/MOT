using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MOT.Application.StudySessions
{
    public class GetStudySessionsQuery : IRequest<List<StudySessionDto>>
    {
    }

    public class GetStudySessionsQueryHandler : IRequestHandler<GetStudySessionsQuery, List<StudySessionDto>>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetStudySessionsQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<StudySessionDto>> Handle(GetStudySessionsQuery request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();
            
            return await _context.StudySessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.StartTime)
                .Select(session => new StudySessionDto
                {
                    Id = session.Id,
                    Title = session.Title,
                    SubjectId = session.SubjectId,
                    SubjectName = session.Subject != null ? session.Subject.Name : null,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    DurationMinutes = session.DurationMinutes,
                    IsCompleted = session.IsCompleted,
                    IsPaused = session.IsPaused,
                    LastPausedAt = session.LastPausedAt,
                    AccumulatedSeconds = session.AccumulatedSeconds,
                    ElapsedSeconds = session.AccumulatedSeconds > 0 ? session.AccumulatedSeconds : session.DurationMinutes * 60
                })
                .ToListAsync(cancellationToken);
        }
    }
}
