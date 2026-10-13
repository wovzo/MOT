using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Gamification;
using MOT.Domain.Entities;
using MOT.Domain.Gamification;

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
        private readonly IGamificationService _gamificationService;
        private readonly IAchievementService _achievementService;

        public ToggleHabitCompletionCommandHandler(
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

        public async Task<bool> Handle(ToggleHabitCompletionCommand request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            using (await UserConcurrencyLock.LockAsync(userId, cancellationToken))
            {
                var habit = await _context.Habits
                    .FirstOrDefaultAsync(h => h.Id == request.HabitId && h.UserId == userId, cancellationToken);

                if (habit == null)
                {
                    throw new HabitNotFoundException(request.HabitId);
                }

                var today = DateTime.UtcNow.Date;
                
                var todayCompletions = await _context.HabitCompletions
                    .Where(c => c.HabitId == request.HabitId && c.Date == today)
                    .ToListAsync(cancellationToken);

                if (todayCompletions.Count > 0)
                {
                    // Already completed today: remove all completions for today (handles any accidental historical duplicates)
                    _context.HabitCompletions.RemoveRange(todayCompletions);
                    await _context.SaveChangesAsync(cancellationToken);
                    return false;
                }

                // Not completed today: attempt to insert completion
                var newCompletion = new HabitCompletion
                {
                    Id = Guid.NewGuid(),
                    HabitId = request.HabitId,
                    Date = today
                };

                _context.HabitCompletions.Add(newCompletion);

                await _gamificationService.AwardHabitXpAsync(userId, habit, today, cancellationToken);
                await _achievementService.EvaluateHabitAchievementsAsync(userId, habit, today, cancellationToken);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return true;
                }
                catch (DbUpdateException ex) when (IsDuplicateCompletionCollision(ex) || _gamificationService.IsXpDuplicateCollision(ex) || _achievementService.IsAchievementDuplicateCollision(ex))
                {
                    // Concurrency race: another transaction already committed completion, XP, or achievement for today.
                    // Detach unpersisted entity and XP transaction, reload user, and return idempotent true.
                    _context.HabitCompletions.Remove(newCompletion);
                    _gamificationService.HandleDuplicateCollision(userId, XpSourceTypes.Habit, $"{habit.Id}:{today:yyyy-MM-dd}");
                    return true;
                }
            }
        }

        private static bool IsDuplicateCompletionCollision(DbUpdateException ex)
        {
            if (ex.InnerException != null)
            {
                var numberProp = ex.InnerException.GetType().GetProperty("Number");
                if (numberProp != null && numberProp.GetValue(ex.InnerException) is int errNumber && errNumber == 1062)
                {
                    return true;
                }
            }

            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("1062") ||
                   msg.Contains("IX_HabitCompletions_HabitId_Date", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("Duplicate", StringComparison.OrdinalIgnoreCase);
        }
    }
}
