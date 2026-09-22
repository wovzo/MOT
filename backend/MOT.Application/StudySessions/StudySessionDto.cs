using System;

namespace MOT.Application.StudySessions
{
    public class StudySessionDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsCompleted { get; set; }
    }
}
