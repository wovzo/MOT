using System;

namespace MOT.Domain.Entities
{
    public class HabitCompletion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid HabitId { get; set; }
        public DateTime Date { get; set; } // The local date the habit was completed (ignoring time)

        // Navigation property
        public Habit Habit { get; set; } = null!;
    }
}
