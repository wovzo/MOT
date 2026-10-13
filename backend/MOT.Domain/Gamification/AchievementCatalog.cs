using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MOT.Domain.Gamification;

public static class AchievementCatalog
{
    private static readonly List<AchievementDefinition> _definitions = new()
    {
        new AchievementDefinition(
            AchievementCodes.FirstStep,
            "First Step",
            "Complete your first daily task",
            "check_circle",
            25),

        new AchievementDefinition(
            AchievementCodes.FocusInitiate,
            "Focus Initiate",
            "Complete your first study session of at least 15 minutes",
            "timer",
            25),

        new AchievementDefinition(
            AchievementCodes.HabitStarter,
            "Habit Starter",
            "Complete your first habit",
            "repeat",
            25),

        new AchievementDefinition(
            AchievementCodes.CenturyClub,
            "Century Club",
            "Reach 100 total minutes of completed focus time",
            "hourglass_full",
            50),

        new AchievementDefinition(
            AchievementCodes.DeepDiver,
            "Deep Diver",
            "Complete a single focus session of at least 60 minutes",
            "scuba_diving",
            50),

        new AchievementDefinition(
            AchievementCodes.StreakBuilder,
            "Streak Builder",
            "Reach a 3-day streak on any habit",
            "local_fire_department",
            50),

        new AchievementDefinition(
            AchievementCodes.Unstoppable,
            "Unstoppable",
            "Reach a 7-day streak on any habit",
            "whatshot",
            100),

        new AchievementDefinition(
            AchievementCodes.TaskMaster,
            "Task Master",
            "Complete 10 distinct daily tasks",
            "task_alt",
            75)
    };

    private static readonly Dictionary<string, AchievementDefinition> _lookup =
        _definitions.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<AchievementDefinition> All { get; } =
        new ReadOnlyCollection<AchievementDefinition>(_definitions);

    public static AchievementDefinition? GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return _lookup.TryGetValue(code, out var def) ? def : null;
    }

    public static bool Contains(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        return _lookup.ContainsKey(code);
    }
}
