using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;
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

        public EndStudySessionCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<StudySessionDto> Handle(EndStudySessionCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            
            var session = await _context.StudySessions
                .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == userId, cancellationToken);
                
            if (session == null)
                throw new Exception("Session not found");
                
            if (!session.IsCompleted)
            {
                session.EndTime = DateTime.UtcNow;
                session.DurationMinutes = (int)(session.EndTime.Value - session.StartTime).TotalMinutes;
                session.IsCompleted = true;
                
                await _context.SaveChangesAsync(cancellationToken);
            }

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
