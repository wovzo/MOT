namespace MOT.Application.Gamification;

public record XpAwardResult(
    bool Awarded,
    int Amount,
    int NewTotalXp,
    int NewLevel,
    bool AlreadyAwarded,
    string? Reason = null
)
{
    public static XpAwardResult Success(int amount, int newTotalXp, int newLevel) =>
        new(true, amount, newTotalXp, newLevel, false);

    public static XpAwardResult Duplicate(int currentXp, int currentLevel) =>
        new(false, 0, currentXp, currentLevel, true, "XP has already been awarded for this event.");

    public static XpAwardResult NotEligible(string reason, int currentXp, int currentLevel) =>
        new(false, 0, currentXp, currentLevel, false, reason);
}
