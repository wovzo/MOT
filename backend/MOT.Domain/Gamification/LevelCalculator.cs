using System;

namespace MOT.Domain.Gamification;

public record LevelProgress(
    int Level,
    int TotalXp,
    long CurrentLevelThresholdXp,
    long NextLevelThresholdXp,
    int XpEarnedInCurrentLevel,
    int XpRequiredForNextLevel,
    double ProgressFraction
);

public static class LevelCalculator
{
    /// <summary>
    /// Calculates the minimum cumulative XP required to reach the specified level.
    /// Formula: Threshold(L) = 50 * L * (L - 1)
    /// Level 1 begins at 0 XP.
    /// Uses 64-bit integer arithmetic to prevent 32-bit overflow.
    /// </summary>
    public static long GetThresholdForLevel(int level)
    {
        if (level <= 1) return 0L;
        long l = level;
        return 50L * l * (l - 1L);
    }

    /// <summary>
    /// Calculates the delta XP required to advance from the specified level to the next level (L -> L + 1).
    /// Formula: Delta(L) = Threshold(L + 1) - Threshold(L) = 100 * L
    /// </summary>
    public static int GetXpRequiredForLevel(int level)
    {
        int safeLevel = Math.Max(1, level);
        return 100 * safeLevel;
    }

    /// <summary>
    /// Determines the level corresponding to the given cumulative XP.
    /// Negative XP is clamped to 0 (Level 1).
    /// Exact 64-bit integer comparisons are used to eliminate floating-point precision error near level boundaries.
    /// </summary>
    public static int CalculateLevel(int totalXp)
    {
        if (totalXp <= 0) return 1;

        long effectiveX = totalXp;

        // Initial approximation from quadratic inversion: L = (1 + sqrt(1 + 2X/25)) / 2
        double approx = (1.0 + Math.Sqrt(1.0 + (2.0 * effectiveX) / 25.0)) / 2.0;
        int candidateLevel = Math.Max(1, (int)Math.Floor(approx));

        // Exact boundary verification and correction using pure 64-bit integer arithmetic
        while (candidateLevel > 1 && effectiveX < GetThresholdForLevel(candidateLevel))
        {
            candidateLevel--;
        }

        while (effectiveX >= GetThresholdForLevel(candidateLevel + 1))
        {
            candidateLevel++;
        }

        return candidateLevel;
    }

    /// <summary>
    /// Computes full level progress metadata for the given total XP.
    /// Negative XP is clamped to 0.
    /// </summary>
    public static LevelProgress GetProgress(int totalXp)
    {
        int clampedXp = Math.Max(0, totalXp);
        int level = CalculateLevel(clampedXp);

        long currentThreshold = GetThresholdForLevel(level);
        long nextThreshold = GetThresholdForLevel(level + 1);

        int xpRequiredInLevel = GetXpRequiredForLevel(level);
        int xpEarnedInLevel = (int)(clampedXp - currentThreshold);

        double fraction = xpRequiredInLevel > 0
            ? Math.Clamp((double)xpEarnedInLevel / xpRequiredInLevel, 0.0, 1.0)
            : 0.0;

        return new LevelProgress(
            Level: level,
            TotalXp: clampedXp,
            CurrentLevelThresholdXp: currentThreshold,
            NextLevelThresholdXp: nextThreshold,
            XpEarnedInCurrentLevel: xpEarnedInLevel,
            XpRequiredForNextLevel: xpRequiredInLevel,
            ProgressFraction: fraction
        );
    }
}
