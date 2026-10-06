using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MOT.Application.StudySessions
{
    public class StartStudySessionCommand : IRequest<StudySessionDto>
    {
        public string Title { get; set; } = "Focus Session";
    }

    public class StartStudySessionCommandHandler : IRequestHandler<StartStudySessionCommand, StudySessionDto>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public StartStudySessionCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<StudySessionDto> Handle(StartStudySessionCommand request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var hasActiveSession = await _context.StudySessions
                .AnyAsync(s => s.UserId == userId && !s.IsCompleted && s.EndTime == null, cancellationToken);

            if (hasActiveSession)
            {
                throw new ActiveStudySessionAlreadyExistsException();
            }

            var session = new StudySession
            {
                UserId = userId,
                Title = request.Title,
                StartTime = DateTime.UtcNow,
                IsCompleted = false
            };

            _context.StudySessions.Add(session);
            await _context.SaveChangesAsync(cancellationToken);

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
