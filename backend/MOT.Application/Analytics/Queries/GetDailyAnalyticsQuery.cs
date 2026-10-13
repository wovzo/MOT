using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Analytics.DTOs;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.Analytics.Queries
{
    public class GetDailyAnalyticsQuery : IRequest<DailyAnalyticsDto>
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class GetDailyAnalyticsQueryHandler : IRequestHandler<GetDailyAnalyticsQuery, DailyAnalyticsDto>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetDailyAnalyticsQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<DailyAnalyticsDto> Handle(GetDailyAnalyticsQuery request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var effectiveEndDate = (request.EndDate?.Date ?? DateTime.UtcNow.Date);
            var effectiveStartDate = (request.StartDate?.Date ?? effectiveEndDate.AddDays(-6));

            if (effectiveStartDate > effectiveEndDate)
            {
                throw new FluentValidation.ValidationException("StartDate cannot be after EndDate.");
            }

            var endExclusive = effectiveEndDate.AddDays(1);

            // Completed study sessions within date range
            var sessions = await _context.StudySessions
                .Where(s => s.UserId == userId && s.EndTime != null && s.StartTime >= effectiveStartDate && s.StartTime < endExclusive)
                .Select(s => new { s.StartTime, s.DurationMinutes })
                .ToListAsync(cancellationToken);

            // Habit completions within date range
            var habitCompletions = await _context.HabitCompletions
                .Where(c => c.Habit.UserId == userId && c.Date >= effectiveStartDate && c.Date <= effectiveEndDate)
                .Select(c => c.Date)
                .ToListAsync(cancellationToken);

            // Build contiguous daily series
            var breakdown = new List<DailyDataPointDto>();
            for (var current = effectiveStartDate; current <= effectiveEndDate; current = current.AddDays(1))
            {
                var dayDate = current.Date;
                var daySessions = sessions.Where(s => s.StartTime.Date == dayDate).ToList();
                var dayFocusMinutes = daySessions.Sum(s => Math.Max(0, s.DurationMinutes));
                var dayCompletedSessions = daySessions.Count;
                var dayHabitCount = habitCompletions.Count(c => c.Date == dayDate);

                breakdown.Add(new DailyDataPointDto
                {
                    Date = dayDate.ToString("yyyy-MM-dd"),
                    FocusMinutes = dayFocusMinutes,
                    CompletedSessionsCount = dayCompletedSessions,
                    HabitCompletionsCount = dayHabitCount
                });
            }

            return new DailyAnalyticsDto
            {
                StartDate = effectiveStartDate.ToString("yyyy-MM-dd"),
                EndDate = effectiveEndDate.ToString("yyyy-MM-dd"),
                TotalFocusMinutes = breakdown.Sum(b => b.FocusMinutes),
                TotalCompletedSessions = breakdown.Sum(b => b.CompletedSessionsCount),
                TotalHabitCompletions = breakdown.Sum(b => b.HabitCompletionsCount),
                DailyBreakdown = breakdown
            };
        }
    }
}
