namespace MOT.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Gamification properties for later phases
    public int XP { get; set; } = 0;
    public int Level { get; set; } = 1;
    public int CurrentStreak { get; set; } = 0;
}
