using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Habits;
using MOT.Domain.Entities;
using MOT.Domain.Gamification;

namespace MOT.Application.Gamification;

public class AchievementService : IAchievementService
{
    private readonly IAppDbContext _context;
    private readonly IGamificationService _gamificationService;

    public AchievementService(IAppDbContext context, IGamificationService gamificationService)
    {
        _context = context;
        _gamificationService = gamificationService;
    }

    public async Task<IReadOnlyList<AchievementDefinition>> EvaluateStudySessionAchievementsAsync(
        Guid userId,
        StudySession session,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        var unlockedCodes = await GetUnlockedAchievementCodesAsync(userId, cancellationToken);
        var newlyUnlocked = new List<AchievementDefinition>();

        // 1. FOCUS_INITIATE: Complete first study session of at least 15 minutes (+25 XP)
        if (!unlockedCodes.Contains(AchievementCodes.FocusInitiate, StringComparer.OrdinalIgnoreCase))
        {
            bool eligible = (session.IsCompleted && session.DurationMinutes >= 15) ||
                await _context.StudySessions.AnyAsync(s => s.UserId == userId && s.IsCompleted && s.EndTime != null && s.DurationMinutes >= 15, cancellationToken);

            if (eligible)
            {
                var def = AchievementCatalog.GetByCode(AchievementCodes.FocusInitiate)!;
                await UnlockAchievementAsync(user, def, newlyUnlocked, cancellationToken);
                unlockedCodes.Add(def.Code);
            }
        }

        // 2. DEEP_DIVER: Complete a single focus session of at least 60 minutes (+50 XP)
        if (!unlockedCodes.Contains(AchievementCodes.DeepDiver, StringComparer.OrdinalIgnoreCase))
        {
            bool eligible = (session.IsCompleted && session.DurationMinutes >= 60) ||
                await _context.StudySessions.AnyAsync(s => s.UserId == userId && s.IsCompleted && s.EndTime != null && s.DurationMinutes >= 60, cancellationToken);

            if (eligible)
            {
                var def = AchievementCatalog.GetByCode(AchievementCodes.DeepDiver)!;
                await UnlockAchievementAsync(user, def, newlyUnlocked, cancellationToken);
                unlockedCodes.Add(def.Code);
            }
        }

        // 3. CENTURY_CLUB: Reach 100 total minutes of completed focus time (+50 XP)
        if (!unlockedCodes.Contains(AchievementCodes.CenturyClub, StringComparer.OrdinalIgnoreCase))
        {
            var priorMinutes = await _context.StudySessions
                .Where(s => s.UserId == userId && s.IsCompleted && s.EndTime != null && s.Id != session.Id)
                .SumAsync(s => s.DurationMinutes, cancellationToken);

            int totalMinutes = priorMinutes + (session.IsCompleted && session.EndTime != null ? session.DurationMinutes : 0);

            if (totalMinutes >= 100)
            {
                var def = AchievementCatalog.GetByCode(AchievementCodes.CenturyClub)!;
                await UnlockAchievementAsync(user, def, newlyUnlocked, cancellationToken);
                unlockedCodes.Add(def.Code);
            }
        }

        return newlyUnlocked;
    }

    public async Task<IReadOnlyList<AchievementDefinition>> EvaluateTaskAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        var unlockedCodes = await GetUnlockedAchievementCodesAsync(userId, cancellationToken);
        var newlyUnlocked = new List<AchievementDefinition>();

        // Query distinct task source IDs in successful XP ledger entries
        var dbTaskSourceIds = await _context.XpTransactions
            .Where(x => x.UserId == userId && x.SourceType == XpSourceTypes.Task)
            .Select(x => x.SourceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        IEnumerable<string> localTaskSourceIds = Enumerable.Empty<string>();
        if (_context is DbContext dbContext)
        {
            localTaskSourceIds = dbContext.ChangeTracker.Entries<XpTransaction>()
                .Where(e => e.Entity.UserId == userId && e.Entity.SourceType == XpSourceTypes.Task && e.State != EntityState.Deleted)
                .Select(e => e.Entity.SourceId);
        }

        var distinctCompletedTasks = dbTaskSourceIds.Union(localTaskSourceIds, StringComparer.OrdinalIgnoreCase).Distinct().ToList();
        int taskCount = distinctCompletedTasks.Count;

        // 1. FIRST_STEP: Complete the first daily task (+25 XP)
        if (!unlockedCodes.Contains(AchievementCodes.FirstStep, StringComparer.OrdinalIgnoreCase))
        {
            if (taskCount >= 1)
            {
                var def = AchievementCatalog.GetByCode(AchievementCodes.FirstStep)!;
                await UnlockAchievementAsync(user, def, newlyUnlocked, cancellationToken);
                unlockedCodes.Add(def.Code);
            }
        }

        // 2. TASK_MASTER: Complete 10 distinct daily tasks (+75 XP)
        if (!unlockedCodes.Contains(AchievementCodes.TaskMaster, StringComparer.OrdinalIgnoreCase))
        {
            if (taskCount >= 10)
            {
                var def = AchievementCatalog.GetByCode(AchievementCodes.TaskMaster)!;
                await UnlockAchievementAsync(user, def, newlyUnlocked, cancellationToken);
                unlockedCodes.Add(def.Code);
            }
        }

        return newlyUnlocked;
    }

    public async Task<IReadOnlyList<AchievementDefinition>> EvaluateHabitAchievementsAsync(
        Guid userId,
        Habit habit,
        DateTime todayUtc,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        var unlockedCodes = await GetUnlockedAchievementCodesAsync(userId, cancellationToken);
        var newlyUnlocked = new List<AchievementDefinition>();

        // 1. HABIT_STARTER: Complete the first habit (+25 XP)
        if (!unlockedCodes.Contains(AchievementCodes.HabitStarter, StringComparer.OrdinalIgnoreCase))
        {
            bool hasDbCompletions = await _context.HabitCompletions
                .AnyAsync(c => _context.Habits.Any(h => h.Id == c.HabitId && h.UserId == userId), cancellationToken);

            bool hasLocalCompletions = false;
            if (_context is DbContext dbContext)
            {
                hasLocalCompletions = dbContext.ChangeTracker.Entries<HabitCompletion>()
                    .Any(e => e.State != EntityState.Deleted);
            }

            if (hasDbCompletions || hasLocalCompletions)
            {
                var def = AchievementCatalog.GetByCode(AchievementCodes.HabitStarter)!;
                await UnlockAchievementAsync(user, def, newlyUnlocked, cancellationToken);
                unlockedCodes.Add(def.Code);
            }
        }

        // 2 & 3: STREAK_BUILDER (3 days, +50 XP) & UNSTOPPABLE (7 days, +100 XP)
        bool needsStreakBuilder = !unlockedCodes.Contains(AchievementCodes.StreakBuilder, StringComparer.OrdinalIgnoreCase);
        bool needsUnstoppable = !unlockedCodes.Contains(AchievementCodes.Unstoppable, StringComparer.OrdinalIgnoreCase);

        if (needsStreakBuilder || needsUnstoppable)
        {
            var completions = await _context.HabitCompletions
                .Where(c => c.HabitId == habit.Id)
                .ToListAsync(cancellationToken);

            if (_context is DbContext dbContext)
            {
                var localCompletions = dbContext.ChangeTracker.Entries<HabitCompletion>()
                    .Where(e => e.Entity.HabitId == habit.Id && e.State != EntityState.Deleted)
                    .Select(e => e.Entity)
                    .ToList();

                completions = completions.Union(localCompletions).ToList();
            }

            var (_, currentStreak) = HabitStreakCalculator.CalculateStreak(completions, todayUtc.Date);

            if (needsStreakBuilder && currentStreak >= 3)
            {
                var def = AchievementCatalog.GetByCode(AchievementCodes.StreakBuilder)!;
                await UnlockAchievementAsync(user, def, newlyUnlocked, cancellationToken);
                unlockedCodes.Add(def.Code);
            }

            if (needsUnstoppable && currentStreak >= 7)
            {
                var def = AchievementCatalog.GetByCode(AchievementCodes.Unstoppable)!;
                await UnlockAchievementAsync(user, def, newlyUnlocked, cancellationToken);
                unlockedCodes.Add(def.Code);
            }
        }

        return newlyUnlocked;
    }

    public bool IsAchievementDuplicateCollision(DbUpdateException ex)
    {
        var fullMsg = $"{ex.Message} {ex.InnerException?.Message}";

        if (ex.InnerException != null)
        {
            var numberProp = ex.InnerException.GetType().GetProperty("Number");
            if (numberProp != null && numberProp.GetValue(ex.InnerException) is int errNumber && errNumber == 1062)
            {
                return fullMsg.Contains("IX_UserAchievements_UserId_AchievementCode", StringComparison.OrdinalIgnoreCase) ||
                       fullMsg.Contains("UserAchievements", StringComparison.OrdinalIgnoreCase);
            }
        }

        return (fullMsg.Contains("1062") || fullMsg.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)) &&
               (fullMsg.Contains("IX_UserAchievements_UserId_AchievementCode", StringComparison.OrdinalIgnoreCase) ||
                fullMsg.Contains("UserAchievements", StringComparison.OrdinalIgnoreCase));
    }

    public void HandleDuplicateCollision(Guid userId, IEnumerable<string> achievementCodes)
    {
        if (_context is DbContext dbContext)
        {
            var codeList = achievementCodes.ToList();
            var entries = dbContext.ChangeTracker.Entries<UserAchievement>()
                .Where(e => e.Entity.UserId == userId && codeList.Contains(e.Entity.AchievementCode, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var entry in entries)
            {
                entry.State = EntityState.Detached;
            }

            foreach (var code in codeList)
            {
                _gamificationService.HandleDuplicateCollision(userId, XpSourceTypes.Achievement, code);
            }

            var userEntry = dbContext.ChangeTracker.Entries<User>()
                .FirstOrDefault(e => e.Entity.Id == userId);

            userEntry?.Reload();
        }
    }

    private async Task<List<string>> GetUnlockedAchievementCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var dbCodes = await _context.UserAchievements
            .Where(a => a.UserId == userId)
            .Select(a => a.AchievementCode)
            .ToListAsync(cancellationToken);

        if (_context is DbContext dbContext)
        {
            var localCodes = dbContext.ChangeTracker.Entries<UserAchievement>()
                .Where(e => e.Entity.UserId == userId && e.State != EntityState.Deleted)
                .Select(e => e.Entity.AchievementCode);

            return dbCodes.Union(localCodes, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return dbCodes;
    }

    private async Task UnlockAchievementAsync(
        User user,
        AchievementDefinition definition,
        List<AchievementDefinition> unlockedList,
        CancellationToken cancellationToken)
    {
        var userAchievement = new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AchievementCode = definition.Code,
            UnlockedAt = DateTime.UtcNow,
            XpAwarded = definition.XpReward
        };
        _context.UserAchievements.Add(userAchievement);

        await _gamificationService.StageAchievementXpAsync(
            user,
            definition.Code,
            definition.XpReward,
            cancellationToken);

        unlockedList.Add(definition);
    }
}
