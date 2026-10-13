using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.Gamification.DTOs;
using MOT.Domain.Gamification;

namespace MOT.Application.Gamification.Queries;

public class GetAchievementsQuery : IRequest<IReadOnlyList<AchievementDto>>
{
}

public class GetAchievementsQueryHandler : IRequestHandler<GetAchievementsQuery, IReadOnlyList<AchievementDto>>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAchievementsQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<AchievementDto>> Handle(GetAchievementsQuery request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            throw new UnauthorizedAccessException();
        }

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId, cancellationToken);

        if (!userExists)
        {
            throw new UserNotFoundException(userId);
        }

        var userAchievements = await _context.UserAchievements
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToDictionaryAsync(a => a.AchievementCode, a => a.UnlockedAt, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var achievements = AchievementCatalog.All.Select(def =>
        {
            bool isUnlocked = userAchievements.TryGetValue(def.Code, out var unlockedAt);
            return new AchievementDto(
                Code: def.Code,
                Title: def.Title,
                Description: def.Description,
                IconName: def.IconName,
                XpReward: def.XpReward,
                IsUnlocked: isUnlocked,
                UnlockedAt: isUnlocked ? unlockedAt : null
            );
        }).ToList();

        return achievements;
    }
}
