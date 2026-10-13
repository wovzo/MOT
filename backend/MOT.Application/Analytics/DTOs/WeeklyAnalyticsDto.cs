using System;
using System.Collections.Generic;

namespace MOT.Application.Analytics.DTOs
{
    public class WeeklyAnalyticsDto
    {
        public int TotalFocusMinutes { get; set; }
        public int TotalCompletedSessions { get; set; }
        public int TotalHabitCompletions { get; set; }
        public List<WeeklyDataPointDto> Weeks { get; set; } = new();
    }

    public class WeeklyDataPointDto
    {
        public string WeekLabel { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public int FocusMinutes { get; set; }
        public int CompletedSessionsCount { get; set; }
        public int HabitCompletionsCount { get; set; }
    }
}
