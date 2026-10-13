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

namespace MOT.Application.Gamification.Queries;

public class GetXpHistoryQuery : IRequest<IReadOnlyList<XpHistoryDto>>
{
    public int Limit { get; set; } = 20;
}

public class GetXpHistoryQueryHandler : IRequestHandler<GetXpHistoryQuery, IReadOnlyList<XpHistoryDto>>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetXpHistoryQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<XpHistoryDto>> Handle(GetXpHistoryQuery request, CancellationToken cancellationToken)
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

        var limit = request.Limit <= 0 ? 20 : request.Limit;

        var history = await _context.XpTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new XpHistoryDto(
                x.Id,
                x.Amount,
                x.SourceType,
                string.IsNullOrWhiteSpace(x.Description) ? x.SourceType : x.Description,
                x.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return history;
    }
}
