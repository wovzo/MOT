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
    public class GetWeeklyAnalyticsQuery : IRequest<WeeklyAnalyticsDto>
    {
        public int Weeks { get; set; } = 4;
        public DateTime? EndDate { get; set; }
    }

    public class GetWeeklyAnalyticsQueryHandler : IRequestHandler<GetWeeklyAnalyticsQuery, WeeklyAnalyticsDto>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetWeeklyAnalyticsQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<WeeklyAnalyticsDto> Handle(GetWeeklyAnalyticsQuery request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var weeksCount = request.Weeks <= 0 ? 4 : request.Weeks;
            var effectiveEndDate = (request.EndDate?.Date ?? DateTime.UtcNow.Date);
            var totalDays = weeksCount * 7;
            var earliestStartDate = effectiveEndDate.AddDays(-totalDays + 1);
            var endExclusive = effectiveEndDate.AddDays(1);

            // Completed study sessions within the entire multi-week period
            var sessions = await _context.StudySessions
                .Where(s => s.UserId == userId && s.EndTime != null && s.StartTime >= earliestStartDate && s.StartTime < endExclusive)
                .Select(s => new { s.StartTime, s.DurationMinutes })
                .ToListAsync(cancellationToken);

            // Habit completions within the period
            var habitCompletions = await _context.HabitCompletions
                .Where(c => c.Habit.UserId == userId && c.Date >= earliestStartDate && c.Date <= effectiveEndDate)
                .Select(c => c.Date)
                .ToListAsync(cancellationToken);

            // Build chronological 7-day intervals
            var weekList = new List<WeeklyDataPointDto>();
            for (int i = weeksCount - 1; i >= 0; i--)
            {
                var wEnd = effectiveEndDate.AddDays(-i * 7);
                var wStart = wEnd.AddDays(-6);
                var wEndExclusive = wEnd.AddDays(1);

                var weekSessions = sessions
                    .Where(s => s.StartTime >= wStart && s.StartTime < wEndExclusive)
                    .ToList();

                var weekCompletions = habitCompletions
                    .Where(c => c.Date >= wStart && c.Date <= wEnd)
                    .ToList();

                var weekNumber = weeksCount - i;
                weekList.Add(new WeeklyDataPointDto
                {
                    WeekLabel = $"Week {weekNumber} ({wStart:MMM dd} - {wEnd:MMM dd})",
                    StartDate = wStart.ToString("yyyy-MM-dd"),
                    EndDate = wEnd.ToString("yyyy-MM-dd"),
                    FocusMinutes = weekSessions.Sum(s => Math.Max(0, s.DurationMinutes)),
                    CompletedSessionsCount = weekSessions.Count,
                    HabitCompletionsCount = weekCompletions.Count
                });
            }

            return new WeeklyAnalyticsDto
            {
                TotalFocusMinutes = weekList.Sum(w => w.FocusMinutes),
                TotalCompletedSessions = weekList.Sum(w => w.CompletedSessionsCount),
                TotalHabitCompletions = weekList.Sum(w => w.HabitCompletionsCount),
                Weeks = weekList
            };
        }
    }
}
