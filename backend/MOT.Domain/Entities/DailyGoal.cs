using System;

namespace MOT.Domain.Entities;

public class DailyGoal
{
    private DateTime _date;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTime Date
    {
        get => _date;
        set => _date = value.Date;
    }

    public int TargetMinutes { get; set; } = 60;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
