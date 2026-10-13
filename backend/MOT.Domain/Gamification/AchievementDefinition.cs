namespace MOT.Domain.Gamification;

public record AchievementDefinition(
    string Code,
    string Title,
    string Description,
    string IconName,
    int XpReward);
