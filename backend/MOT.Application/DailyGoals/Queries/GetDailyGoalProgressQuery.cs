using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;
using MOT.Application.DailyGoals.DTOs;

namespace MOT.Application.DailyGoals.Queries;

public class GetDailyGoalProgressQuery : IRequest<DailyGoalProgressDto>
{
    public string? Date { get; set; }
    public int? TimeZoneOffsetMinutes { get; set; }
}

public class GetDailyGoalProgressQueryHandler : IRequestHandler<GetDailyGoalProgressQuery, DailyGoalProgressDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetDailyGoalProgressQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<DailyGoalProgressDto> Handle(GetDailyGoalProgressQuery request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedAccessException();

        var offset = request.TimeZoneOffsetMinutes ?? 0;

        DateTime targetDate;
        if (!string.IsNullOrWhiteSpace(request.Date))
        {
            if (!DateTime.TryParseExact(request.Date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out targetDate))
            {
                throw new FluentValidation.ValidationException("Date must be in yyyy-MM-dd format.");
            }
        }
        else
        {
            targetDate = DateTime.UtcNow.AddMinutes(offset).Date;
        }

        // Look up configured target or default to 60 minutes
        var goal = await _context.DailyGoals
            .FirstOrDefaultAsync(g => g.UserId == userId && g.Date == targetDate, cancellationToken);

        var targetMinutes = goal?.TargetMinutes ?? 60;
        var isCustom = goal != null;

        // Window in UTC for the target calendar date given client timezone offset
        var utcStart = targetDate.AddMinutes(-offset);
        var utcEnd = utcStart.AddDays(1);

        // Fetch completed sessions strictly within the window (excluding unfinished or paused-only sessions)
        var sessions = await _context.StudySessions
            .Where(s => s.UserId == userId && s.IsCompleted && s.EndTime != null && s.StartTime >= utcStart && s.StartTime < utcEnd)
            .Select(s => s.DurationMinutes)
            .ToListAsync(cancellationToken);

        var actualMinutes = sessions.Sum(d => Math.Max(0, d));
        var completedCount = sessions.Count;

        var remainingMinutes = Math.Max(0, targetMinutes - actualMinutes);
        var progressPercentage = targetMinutes > 0
            ? Math.Min(100.0, Math.Round((double)actualMinutes / targetMinutes * 100.0, 1))
            : 100.0;
        var isAchieved = actualMinutes >= targetMinutes;

        return new DailyGoalProgressDto
        {
            Date = targetDate.ToString("yyyy-MM-dd"),
            TargetMinutes = targetMinutes,
            ActualMinutes = actualMinutes,
            RemainingMinutes = remainingMinutes,
            ProgressPercentage = progressPercentage,
            IsAchieved = isAchieved,
            IsCustomTarget = isCustom,
            CompletedSessionsCount = completedCount
        };
    }
}
