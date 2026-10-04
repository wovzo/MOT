using System;

namespace MOT.Application.DTOs
{
    public record UserProfileDto(
        Guid Id,
        string Email,
        string DisplayName,
        DateTime CreatedAt,
        int CurrentStreak,
        int Level,
        int XP,
        int TotalFocusMinutes
    );
}
