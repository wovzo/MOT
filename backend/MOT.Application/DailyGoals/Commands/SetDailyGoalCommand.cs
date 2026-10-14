using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;
using MOT.Application.DailyGoals.DTOs;
using MOT.Domain.Entities;

namespace MOT.Application.DailyGoals.Commands;

public class SetDailyGoalCommand : IRequest<DailyGoalProgressDto>
{
    public string? Date { get; set; }
    public int TargetMinutes { get; set; }
    public int? TimeZoneOffsetMinutes { get; set; }
}

public class SetDailyGoalCommandHandler : IRequestHandler<SetDailyGoalCommand, DailyGoalProgressDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SetDailyGoalCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<DailyGoalProgressDto> Handle(SetDailyGoalCommand request, CancellationToken cancellationToken)
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

        var goal = await _context.DailyGoals
            .FirstOrDefaultAsync(g => g.UserId == userId && g.Date == targetDate, cancellationToken);

        if (goal == null)
        {
            goal = new DailyGoal
            {
                UserId = userId,
                Date = targetDate,
                TargetMinutes = request.TargetMinutes,
                CreatedAt = DateTime.UtcNow
            };
            _context.DailyGoals.Add(goal);
        }
        else
        {
            goal.TargetMinutes = request.TargetMinutes;
            goal.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Calculate progress for the updated target
        var utcStart = targetDate.AddMinutes(-offset);
        var utcEnd = utcStart.AddDays(1);

        var sessions = await _context.StudySessions
            .Where(s => s.UserId == userId && s.IsCompleted && s.EndTime != null && s.StartTime >= utcStart && s.StartTime < utcEnd)
            .Select(s => s.DurationMinutes)
            .ToListAsync(cancellationToken);

        var actualMinutes = sessions.Sum(d => Math.Max(0, d));
        var completedCount = sessions.Count;

        var remainingMinutes = Math.Max(0, goal.TargetMinutes - actualMinutes);
        var progressPercentage = goal.TargetMinutes > 0
            ? Math.Min(100.0, Math.Round((double)actualMinutes / goal.TargetMinutes * 100.0, 1))
            : 100.0;
        var isAchieved = actualMinutes >= goal.TargetMinutes;

        return new DailyGoalProgressDto
        {
            Date = targetDate.ToString("yyyy-MM-dd"),
            TargetMinutes = goal.TargetMinutes,
            ActualMinutes = actualMinutes,
            RemainingMinutes = remainingMinutes,
            ProgressPercentage = progressPercentage,
            IsAchieved = isAchieved,
            IsCustomTarget = true,
            CompletedSessionsCount = completedCount
        };
    }
}
