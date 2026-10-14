using System;

namespace MOT.Application.StudySessions
{
    public class StudySessionDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public Guid? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsPaused { get; set; }
        public DateTime? LastPausedAt { get; set; }
        public int AccumulatedSeconds { get; set; }
        public int ElapsedSeconds { get; set; }
    }
}
