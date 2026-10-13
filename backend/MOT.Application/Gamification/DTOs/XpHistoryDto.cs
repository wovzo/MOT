using System;

namespace MOT.Application.Gamification.DTOs;

public record XpHistoryDto(
    Guid Id,
    int Amount,
    string SourceType,
    string Description,
    DateTime CreatedAt
);
