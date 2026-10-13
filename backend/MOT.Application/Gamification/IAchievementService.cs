using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MOT.Domain.Entities;
using MOT.Domain.Gamification;

namespace MOT.Application.Gamification;

public interface IAchievementService
{
    Task<IReadOnlyList<AchievementDefinition>> EvaluateStudySessionAchievementsAsync(
        Guid userId,
        StudySession session,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AchievementDefinition>> EvaluateTaskAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AchievementDefinition>> EvaluateHabitAchievementsAsync(
        Guid userId,
        Habit habit,
        DateTime todayUtc,
        CancellationToken cancellationToken = default);

    bool IsAchievementDuplicateCollision(DbUpdateException ex);
    void HandleDuplicateCollision(Guid userId, IEnumerable<string> achievementCodes);
}
