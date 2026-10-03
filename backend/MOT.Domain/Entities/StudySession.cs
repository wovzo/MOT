using System;

namespace MOT.Domain.Entities
{
    public class StudySession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public User? User { get; set; }
        public string Title { get; set; } = "Focus Session";
        
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        
        public int DurationMinutes { get; set; } = 0;
        public bool IsCompleted { get; set; } = false;
    }
}
