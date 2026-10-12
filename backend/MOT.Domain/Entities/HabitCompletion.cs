using System;

namespace MOT.Domain.Entities
{
    public class HabitCompletion
    {
        private DateTime _date;

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid HabitId { get; set; }
        public DateTime Date
        {
            get => _date;
            set => _date = value.Date;
        }

        // Navigation property
        public Habit Habit { get; set; } = null!;
    }
}
