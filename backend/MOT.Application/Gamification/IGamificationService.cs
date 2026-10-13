using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;

namespace MOT.Application.Gamification;

public interface IGamificationService
{
    int CalculateStudySessionXp(int durationMinutes);
    int TaskCompletionXp { get; }
    int HabitCompletionXp { get; }

    Task<XpAwardResult> AwardStudySessionXpAsync(
        Guid userId,
        StudySession session,
        CancellationToken cancellationToken = default);

    Task<XpAwardResult> AwardTaskXpAsync(
        Guid userId,
        DailyTask task,
        CancellationToken cancellationToken = default);

    Task<XpAwardResult> AwardHabitXpAsync(
        Guid userId,
        Habit habit,
        DateTime dateUtc,
        CancellationToken cancellationToken = default);

    Task<XpAwardResult> AwardXpDirectAsync(
        Guid userId,
        int amount,
        string sourceType,
        string sourceId,
        string description,
        CancellationToken cancellationToken = default);

    Task<XpAwardResult> StageAchievementXpAsync(
        User user,
        string achievementCode,
        int xpReward,
        CancellationToken cancellationToken = default);

    bool IsXpDuplicateCollision(DbUpdateException ex);
    void HandleDuplicateCollision(Guid userId, string sourceType, string sourceId);
}
