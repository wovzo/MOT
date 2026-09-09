using System;
using System.Collections.Generic;

namespace MOT.Domain.Entities
{
    public class Habit
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;
        public ICollection<HabitCompletion> Completions { get; set; } = new List<HabitCompletion>();
    }
}
