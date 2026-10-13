namespace MOT.Application.Gamification.DTOs;

public record GamificationSummaryDto(
    int Level,
    int TotalXp,
    long CurrentLevelThresholdXp,
    long NextLevelThresholdXp,
    int XpEarnedInCurrentLevel,
    int XpRequiredForNextLevel,
    double ProgressFraction,
    double ProgressPercentage,
    int TotalAchievements,
    int UnlockedAchievements
);
