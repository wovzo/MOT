using System;

namespace MOT.Application.DTOs
{
    public class HabitDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsCompletedToday { get; set; }
        public int CurrentStreak { get; set; }
    }
}
