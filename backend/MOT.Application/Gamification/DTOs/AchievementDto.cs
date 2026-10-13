using System;

namespace MOT.Application.Gamification.DTOs;

public record AchievementDto(
    string Code,
    string Title,
    string Description,
    string IconName,
    int XpReward,
    bool IsUnlocked,
    DateTime? UnlockedAt
);
