using System;

namespace MOT.Application.DailyGoals.DTOs;

public class DailyGoalProgressDto
{
    public string Date { get; set; } = string.Empty;
    public int TargetMinutes { get; set; } = 60;
    public int ActualMinutes { get; set; } = 0;
    public int RemainingMinutes { get; set; } = 60;
    public double ProgressPercentage { get; set; } = 0.0;
    public bool IsAchieved { get; set; } = false;
    public bool IsCustomTarget { get; set; } = false;
    public int CompletedSessionsCount { get; set; } = 0;
}
