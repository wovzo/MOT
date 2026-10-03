using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;

namespace MOT.Application.Habits.Commands
{
    public class ToggleHabitCompletionCommand : IRequest<bool>
    {
        public Guid HabitId { get; set; }
    }

    public class ToggleHabitCompletionCommandHandler : IRequestHandler<ToggleHabitCompletionCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ToggleHabitCompletionCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<bool> Handle(ToggleHabitCompletionCommand request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var habit = await _context.Habits
                .FirstOrDefaultAsync(h => h.Id == request.HabitId && h.UserId == userId, cancellationToken);

            if (habit == null)
            {
                throw new HabitNotFoundException(request.HabitId);
            }

            var today = DateTime.UtcNow.Date;
            
            var completion = await _context.HabitCompletions
                .FirstOrDefaultAsync(c => c.HabitId == request.HabitId && c.Date == today, cancellationToken);

            bool isCompletedNow;

            if (completion != null)
            {
                // Already completed today, so un-complete it
                _context.HabitCompletions.Remove(completion);
                isCompletedNow = false;
            }
            else
            {
                // Not completed today, so complete it
                _context.HabitCompletions.Add(new HabitCompletion
                {
                    HabitId = request.HabitId,
                    Date = today
                });
                isCompletedNow = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return isCompletedNow;
        }
    }
}
