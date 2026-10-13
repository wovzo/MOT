using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MOT.Application.Common;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;
using MOT.Domain.Gamification;

namespace MOT.Application.Gamification;

public class GamificationService : IGamificationService
{
    private readonly IAppDbContext _context;
    private readonly GamificationOptions _options;

    public GamificationService(IAppDbContext context, IOptions<GamificationOptions>? options = null)
    {
        _context = context;
        _options = options?.Value ?? new GamificationOptions();
    }

    public int TaskCompletionXp => _options.TaskCompletionXp;
    public int HabitCompletionXp => _options.HabitCompletionXp;

    public int CalculateStudySessionXp(int durationMinutes)
    {
        if (durationMinutes < _options.StudySessionMinMinutes)
        {
            return 0;
        }

        int bonusFromDuration = durationMinutes / _options.StudySessionMinutesPerXp;
        int totalXp = _options.StudySessionBaseXp + bonusFromDuration;

        return Math.Min(_options.StudySessionMaxXp, totalXp);
    }

    public async Task<XpAwardResult> AwardStudySessionXpAsync(
        Guid userId,
        StudySession session,
        CancellationToken cancellationToken = default)
    {
        if (session.UserId != userId)
        {
            throw new UnauthorizedAccessException("Session does not belong to the authenticated user.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        if (!session.IsCompleted)
        {
            return XpAwardResult.NotEligible("Session is not completed.", user.XP, user.Level);
        }

        int amount = CalculateStudySessionXp(session.DurationMinutes);
        if (amount <= 0)
        {
            return XpAwardResult.NotEligible(
                $"Session duration ({session.DurationMinutes}m) is below the minimum required duration ({_options.StudySessionMinMinutes}m).",
                user.XP,
                user.Level);
        }

        return await StageXpAwardInternalAsync(
            user,
            amount,
            XpSourceTypes.StudySession,
            session.Id.ToString(),
            $"Completed {session.DurationMinutes}-minute study session",
            cancellationToken);
    }

    public async Task<XpAwardResult> AwardTaskXpAsync(
        Guid userId,
        DailyTask task,
        CancellationToken cancellationToken = default)
    {
        if (task.UserId != userId)
        {
            throw new UnauthorizedAccessException("Task does not belong to the authenticated user.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        if (!task.IsCompleted)
        {
            return XpAwardResult.NotEligible("Task is not completed.", user.XP, user.Level);
        }

        return await StageXpAwardInternalAsync(
            user,
            _options.TaskCompletionXp,
            XpSourceTypes.Task,
            task.Id.ToString(),
            $"Completed task: {task.Title}",
            cancellationToken);
    }

    public async Task<XpAwardResult> AwardHabitXpAsync(
        Guid userId,
        Habit habit,
        DateTime dateUtc,
        CancellationToken cancellationToken = default)
    {
        if (habit.UserId != userId)
        {
            throw new UnauthorizedAccessException("Habit does not belong to the authenticated user.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        var dateString = dateUtc.Date.ToString("yyyy-MM-dd");
        var sourceId = $"{habit.Id}:{dateString}";

        return await StageXpAwardInternalAsync(
            user,
            _options.HabitCompletionXp,
            XpSourceTypes.Habit,
            sourceId,
            $"Completed habit: {habit.Title} on {dateString}",
            cancellationToken);
    }

    public async Task<XpAwardResult> AwardXpDirectAsync(
        Guid userId,
        int amount,
        string sourceType,
        string sourceId,
        string description,
        CancellationToken cancellationToken = default)
    {
        using (await UserConcurrencyLock.LockAsync(userId, cancellationToken))
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                ?? throw new UserNotFoundException(userId);

            var result = await StageXpAwardInternalAsync(user, amount, sourceType, sourceId, description, cancellationToken);
            if (result.AlreadyAwarded || !result.Awarded)
            {
                return result;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException ex) when (IsXpDuplicateCollision(ex))
            {
                HandleDuplicateCollision(userId, sourceType, sourceId);
                return XpAwardResult.Duplicate(user.XP, user.Level);
            }
        }
    }

    public async Task<XpAwardResult> StageAchievementXpAsync(
        User user,
        string achievementCode,
        int xpReward,
        CancellationToken cancellationToken = default)
    {
        return await StageXpAwardInternalAsync(
            user,
            xpReward,
            XpSourceTypes.Achievement,
            achievementCode,
            $"Unlocked achievement: {achievementCode}",
            cancellationToken);
    }

    private async Task<XpAwardResult> StageXpAwardInternalAsync(
        User user,
        int amount,
        string sourceType,
        string sourceId,
        string description,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return XpAwardResult.NotEligible("Award amount must be greater than zero.", user.XP, user.Level);
        }

        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new ArgumentException("Source type must be provided.", nameof(sourceType));
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source ID must be provided.", nameof(sourceId));
        }

        // 1. Check idempotency: check if already awarded
        var alreadyAwarded = await _context.XpTransactions.AnyAsync(
            x => x.UserId == user.Id && x.SourceType == sourceType && x.SourceId == sourceId,
            cancellationToken);

        if (alreadyAwarded)
        {
            return XpAwardResult.Duplicate(user.XP, user.Level);
        }

        // 2. Prevent integer overflow on balance increment
        if ((long)user.XP + amount > int.MaxValue)
        {
            throw new OverflowException($"XP balance overflow: adding {amount} to user {user.Id} with current XP {user.XP} exceeds int.MaxValue.");
        }

        // 3. Stage ledger record
        var transaction = new XpTransaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Amount = amount,
            SourceType = sourceType,
            SourceId = sourceId,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        _context.XpTransactions.Add(transaction);

        // 4. Update user balance and recalculate level
        user.XP += amount;
        user.Level = LevelCalculator.CalculateLevel(user.XP);

        return XpAwardResult.Success(amount, user.XP, user.Level);
    }

    public bool IsXpDuplicateCollision(DbUpdateException ex)
    {
        var fullMsg = $"{ex.Message} {ex.InnerException?.Message}";

        if (ex.InnerException != null)
        {
            var numberProp = ex.InnerException.GetType().GetProperty("Number");
            if (numberProp != null && numberProp.GetValue(ex.InnerException) is int errNumber && errNumber == 1062)
            {
                return fullMsg.Contains("IX_XpTransactions_UserId_SourceType_SourceId", StringComparison.OrdinalIgnoreCase) ||
                       fullMsg.Contains("XpTransactions", StringComparison.OrdinalIgnoreCase);
            }
        }

        return (fullMsg.Contains("1062") || fullMsg.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)) &&
               (fullMsg.Contains("IX_XpTransactions_UserId_SourceType_SourceId", StringComparison.OrdinalIgnoreCase) ||
                fullMsg.Contains("XpTransactions", StringComparison.OrdinalIgnoreCase));
    }

    public void HandleDuplicateCollision(Guid userId, string sourceType, string sourceId)
    {
        if (_context is DbContext dbContext)
        {
            var entries = dbContext.ChangeTracker.Entries<XpTransaction>()
                .Where(e => e.Entity.UserId == userId && e.Entity.SourceType == sourceType && e.Entity.SourceId == sourceId)
                .ToList();

            foreach (var entry in entries)
            {
                entry.State = EntityState.Detached;
            }

            var userEntry = dbContext.ChangeTracker.Entries<User>()
                .FirstOrDefault(e => e.Entity.Id == userId);

            if (userEntry != null)
            {
                userEntry.Reload();
            }
        }
    }
}
