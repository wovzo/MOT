using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Gamification.DTOs;
using MOT.Domain.Gamification;

namespace MOT.Application.Gamification.Queries;

public class GetGamificationSummaryQuery : IRequest<GamificationSummaryDto>
{
}

public class GetGamificationSummaryQueryHandler : IRequestHandler<GetGamificationSummaryQuery, GamificationSummaryDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetGamificationSummaryQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<GamificationSummaryDto> Handle(GetGamificationSummaryQuery request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        var progress = LevelCalculator.GetProgress(user.XP);

        var unlockedCount = await _context.UserAchievements
            .CountAsync(a => a.UserId == userId, cancellationToken);

        return new GamificationSummaryDto(
            Level: progress.Level,
            TotalXp: progress.TotalXp,
            CurrentLevelThresholdXp: progress.CurrentLevelThresholdXp,
            NextLevelThresholdXp: progress.NextLevelThresholdXp,
            XpEarnedInCurrentLevel: progress.XpEarnedInCurrentLevel,
            XpRequiredForNextLevel: progress.XpRequiredForNextLevel,
            ProgressFraction: progress.ProgressFraction,
            ProgressPercentage: Math.Round(progress.ProgressFraction * 100.0, 1),
            TotalAchievements: AchievementCatalog.All.Count,
            UnlockedAchievements: unlockedCount
        );
    }
}
