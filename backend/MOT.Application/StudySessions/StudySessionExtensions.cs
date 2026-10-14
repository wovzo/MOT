using System;
using MOT.Domain.Entities;

namespace MOT.Application.StudySessions;

public static class StudySessionExtensions
{
    public static StudySessionDto ToDto(this StudySession session, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        int elapsedSeconds;

        if (session.IsCompleted)
        {
            elapsedSeconds = session.AccumulatedSeconds > 0
                ? session.AccumulatedSeconds
                : session.DurationMinutes * 60;
        }
        else if (session.IsPaused)
        {
            elapsedSeconds = session.AccumulatedSeconds;
        }
        else
        {
            var resumedAt = session.LastResumedAt ?? session.StartTime;
            var currentSegmentSeconds = Math.Max(0, (int)(now - resumedAt).TotalSeconds);
            elapsedSeconds = session.AccumulatedSeconds + currentSegmentSeconds;
        }

        return new StudySessionDto
        {
            Id = session.Id,
            Title = session.Title,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            DurationMinutes = session.IsCompleted ? session.DurationMinutes : Math.Max(0, elapsedSeconds / 60),
            IsCompleted = session.IsCompleted,
            IsPaused = session.IsPaused,
            LastPausedAt = session.LastPausedAt,
            AccumulatedSeconds = session.AccumulatedSeconds,
            ElapsedSeconds = elapsedSeconds
        };
    }
}
