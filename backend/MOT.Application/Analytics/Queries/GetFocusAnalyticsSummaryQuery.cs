using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Analytics.DTOs;
using MOT.Application.Common.Interfaces;
using MOT.Application.Habits;

namespace MOT.Application.Analytics.Queries
{
    public class GetFocusAnalyticsSummaryQuery : IRequest<FocusAnalyticsSummaryDto>
    {
        public DateTime? TodayOverride { get; set; }
    }

    public class GetFocusAnalyticsSummaryQueryHandler : IRequestHandler<GetFocusAnalyticsSummaryQuery, FocusAnalyticsSummaryDto>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetFocusAnalyticsSummaryQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<FocusAnalyticsSummaryDto> Handle(GetFocusAnalyticsSummaryQuery request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var todayUtc = request.TodayOverride?.Date ?? DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);
            var weekStartUtc = todayUtc.AddDays(-6); // 7-day rolling window: [today - 6 days, today]

            // Study Sessions Aggregations
            var totalFocusMinutes = await _context.StudySessions
                .Where(s => s.UserId == userId && s.EndTime != null)
                .SumAsync(s => s.DurationMinutes, cancellationToken);

            var todayFocusMinutes = await _context.StudySessions
                .Where(s => s.UserId == userId && s.EndTime != null && s.StartTime >= todayUtc && s.StartTime < tomorrowUtc)
                .SumAsync(s => s.DurationMinutes, cancellationToken);

            var thisWeekFocusMinutes = await _context.StudySessions
                .Where(s => s.UserId == userId && s.EndTime != null && s.StartTime >= weekStartUtc && s.StartTime < tomorrowUtc)
                .SumAsync(s => s.DurationMinutes, cancellationToken);

            var totalSessions = await _context.StudySessions
                .CountAsync(s => s.UserId == userId, cancellationToken);

            var completedSessions = await _context.StudySessions
                .CountAsync(s => s.UserId == userId && s.EndTime != null && s.IsCompleted, cancellationToken);

            var incompleteSessions = totalSessions - completedSessions;

            var averageSessionMinutes = completedSessions > 0
                ? Math.Round((double)totalFocusMinutes / completedSessions, 1)
                : 0.0;

            // Tasks Aggregations
            var totalTasks = await _context.Tasks
                .CountAsync(t => t.UserId == userId, cancellationToken);

            var completedTasks = await _context.Tasks
                .CountAsync(t => t.UserId == userId && t.IsCompleted, cancellationToken);

            var pendingTasks = totalTasks - completedTasks;

            var taskCompletionRate = totalTasks > 0
                ? Math.Round((double)completedTasks / totalTasks * 100.0, 1)
                : 0.0;

            // Habits & Completions Aggregations
            var habits = await _context.Habits
                .Include(h => h.Completions)
                .Where(h => h.UserId == userId)
                .ToListAsync(cancellationToken);

            var totalHabits = habits.Count;
            var totalHabitCompletions = habits.Sum(h => h.Completions.Count);
            var habitCompletionsToday = habits.Sum(h => h.Completions.Count(c => c.Date.Date == todayUtc));
            var habitCompletionsThisWeek = habits.Sum(h => h.Completions.Count(c => c.Date.Date >= weekStartUtc && c.Date.Date <= todayUtc));
            var activeStreaksCount = habits.Count(h => HabitStreakCalculator.CalculateStreak(h.Completions, todayUtc).CurrentStreak >= 1);

            return new FocusAnalyticsSummaryDto
            {
                TotalFocusMinutes = totalFocusMinutes,
                TodayFocusMinutes = todayFocusMinutes,
                ThisWeekFocusMinutes = thisWeekFocusMinutes,
                TotalSessionsCount = totalSessions,
                CompletedSessionsCount = completedSessions,
                IncompleteSessionsCount = incompleteSessions,
                AverageSessionMinutes = averageSessionMinutes,
                Tasks = new TaskAnalyticsSummaryDto
                {
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    PendingTasks = pendingTasks,
                    CompletionRatePercentage = taskCompletionRate
                },
                Habits = new HabitAnalyticsSummaryDto
                {
                    TotalHabits = totalHabits,
                    TotalCompletions = totalHabitCompletions,
                    CompletionsToday = habitCompletionsToday,
                    CompletionsThisWeek = habitCompletionsThisWeek,
                    ActiveStreaksCount = activeStreaksCount
                }
            };
        }
    }
}
