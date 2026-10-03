using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MOT.Application.StudySessions
{
    public class GetActiveStudySessionQuery : IRequest<StudySessionDto?>
    {
    }

    public class GetActiveStudySessionQueryHandler : IRequestHandler<GetActiveStudySessionQuery, StudySessionDto?>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetActiveStudySessionQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<StudySessionDto?> Handle(GetActiveStudySessionQuery request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var session = await _context.StudySessions
                .Where(s => s.UserId == userId && !s.IsCompleted)
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync(cancellationToken);

            if (session == null)
                return null;

            return new StudySessionDto
            {
                Id = session.Id,
                Title = session.Title,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                DurationMinutes = session.DurationMinutes,
                IsCompleted = session.IsCompleted
            };
        }
    }
}
