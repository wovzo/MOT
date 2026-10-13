using System;

namespace MOT.Domain.Entities;

public class XpTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Foreign key for the User
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public int Amount { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
