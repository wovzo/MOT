using System;
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

namespace MOT.Application.Tasks.Commands
{
    public class ToggleTaskCommand : IRequest<bool>
    {
        public Guid Id { get; set; }

        public ToggleTaskCommand() { }

        public ToggleTaskCommand(Guid id)
        {
            Id = id;
        }
    }

    public class ToggleTaskCommandHandler : IRequestHandler<ToggleTaskCommand, bool>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IGamificationService _gamificationService;
        private readonly IAchievementService _achievementService;

        public ToggleTaskCommandHandler(
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

        public async Task<bool> Handle(ToggleTaskCommand request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            using (await UserConcurrencyLock.LockAsync(userId, cancellationToken))
            {
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == request.Id && t.UserId == userId, cancellationToken);

                if (task == null)
                {
                    throw new TaskNotFoundException(request.Id);
                }

                task.IsCompleted = !task.IsCompleted;

                if (task.IsCompleted)
                {
                    await _gamificationService.AwardTaskXpAsync(userId, task, cancellationToken);
                    await _achievementService.EvaluateTaskAchievementsAsync(userId, cancellationToken);
                }

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (_gamificationService.IsXpDuplicateCollision(ex) || _achievementService.IsAchievementDuplicateCollision(ex))
                {
                    _gamificationService.HandleDuplicateCollision(userId, XpSourceTypes.Task, task.Id.ToString());
                    if (_context is DbContext dbContext)
                    {
                        var taskEntry = dbContext.ChangeTracker.Entries<DailyTask>().FirstOrDefault(e => e.Entity.Id == task.Id);
                        taskEntry?.Reload();
                    }
                }

                return task.IsCompleted;
            }
        }
    }
}
