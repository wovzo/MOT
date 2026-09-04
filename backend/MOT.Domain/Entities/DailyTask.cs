namespace MOT.Domain.Entities;

public class DailyTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Foreign Key for the User
    public Guid UserId { get; set; }
    public User? User { get; set; }
}
