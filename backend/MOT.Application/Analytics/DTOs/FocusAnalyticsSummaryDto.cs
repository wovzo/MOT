using System;

namespace MOT.Application.Analytics.DTOs
{
    public class FocusAnalyticsSummaryDto
    {
        public int TotalFocusMinutes { get; set; }
        public int TodayFocusMinutes { get; set; }
        public int ThisWeekFocusMinutes { get; set; }
        public int TotalSessionsCount { get; set; }
        public int CompletedSessionsCount { get; set; }
        public int IncompleteSessionsCount { get; set; }
        public double AverageSessionMinutes { get; set; }
        public TaskAnalyticsSummaryDto Tasks { get; set; } = new();
        public HabitAnalyticsSummaryDto Habits { get; set; } = new();
    }

    public class TaskAnalyticsSummaryDto
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public double CompletionRatePercentage { get; set; }
    }

    public class HabitAnalyticsSummaryDto
    {
        public int TotalHabits { get; set; }
        public int TotalCompletions { get; set; }
        public int CompletionsToday { get; set; }
        public int CompletionsThisWeek { get; set; }
        public int ActiveStreaksCount { get; set; }
    }
}
