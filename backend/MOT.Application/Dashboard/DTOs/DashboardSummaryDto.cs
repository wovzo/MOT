using System;
using System.Collections.Generic;
using MOT.Application.DailyGoals.DTOs;
using MOT.Application.DTOs;
using MOT.Application.Gamification.DTOs;
using MOT.Application.StudySessions;
using MOT.Application.Subjects.DTOs;
using MOT.Application.Tasks.Queries;

namespace MOT.Application.Dashboard.DTOs;

public class DashboardSummaryDto
{
    public DailyGoalProgressDto DailyGoal { get; set; } = null!;
    public StudySessionDto? ActiveSession { get; set; }
    public DashboardTaskSummaryDto Tasks { get; set; } = null!;
    public DashboardHabitSummaryDto Habits { get; set; } = null!;
    public GamificationSummaryDto Gamification { get; set; } = null!;
    public DashboardStreakSummaryDto Streak { get; set; } = null!;
    public List<SubjectDto> Subjects { get; set; } = new();
    public List<StudySessionDto> RecentSessions { get; set; } = new();
    public List<DashboardDayActivityDto> WeeklyActivity { get; set; } = new();
}

public class DashboardTaskSummaryDto
{
    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int PendingCount { get; set; }
    public double CompletionPercentage { get; set; }
    public List<DailyTaskDto> TopPendingTasks { get; set; } = new();
}

public class DashboardHabitSummaryDto
{
    public int TotalCount { get; set; }
    public int CompletedTodayCount { get; set; }
    public double CompletionPercentage { get; set; }
    public List<HabitDto> Habits { get; set; } = new();
}

public class DashboardStreakSummaryDto
{
    public int CurrentStreak { get; set; }
    public bool IsActiveToday { get; set; }
    public int LongestStreak { get; set; }
}

public class DashboardDayActivityDto
{
    public string Date { get; set; } = string.Empty;
    public string DayLabel { get; set; } = string.Empty;
    public int FocusMinutes { get; set; }
    public int CompletedSessionsCount { get; set; }
}
