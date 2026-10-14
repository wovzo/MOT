using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.DailyGoals.DTOs;
using MOT.Application.Dashboard.DTOs;
using MOT.Application.DTOs;
using MOT.Application.Gamification.DTOs;
using MOT.Application.Habits;
using MOT.Application.StudySessions;
using MOT.Application.Subjects.DTOs;
using MOT.Application.Tasks.Queries;
using MOT.Domain.Gamification;

namespace MOT.Application.Dashboard.Queries;

public class GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>
{
    public int? TimeZoneOffsetMinutes { get; set; }
}

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetDashboardSummaryQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedAccessException();

        var offset = request.TimeZoneOffsetMinutes ?? 0;
        var nowUtc = DateTime.UtcNow;
        var targetDate = nowUtc.AddMinutes(offset).Date;
        var utcStart = targetDate.AddMinutes(-offset);
        var utcEnd = utcStart.AddDays(1);

        // 1. User & Gamification
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
            throw new UserNotFoundException(userId);

        var xpProgress = LevelCalculator.GetProgress(user.XP);
        var unlockedAchievements = await _context.UserAchievements
            .CountAsync(a => a.UserId == userId, cancellationToken);

        var gamificationDto = new GamificationSummaryDto(
            Level: xpProgress.Level,
            TotalXp: xpProgress.TotalXp,
            CurrentLevelThresholdXp: xpProgress.CurrentLevelThresholdXp,
            NextLevelThresholdXp: xpProgress.NextLevelThresholdXp,
            XpEarnedInCurrentLevel: xpProgress.XpEarnedInCurrentLevel,
            XpRequiredForNextLevel: xpProgress.XpRequiredForNextLevel,
            ProgressFraction: xpProgress.ProgressFraction,
            ProgressPercentage: Math.Round(xpProgress.ProgressFraction * 100.0, 1),
            TotalAchievements: AchievementCatalog.All.Count,
            UnlockedAchievements: unlockedAchievements
        );

        // 2. Daily Goal & Today's Focus
        var goal = await _context.DailyGoals
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == userId && g.Date == targetDate, cancellationToken);

        var targetMinutes = goal?.TargetMinutes ?? 60;
        var isCustomTarget = goal != null;

        var todaySessions = await _context.StudySessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsCompleted && s.EndTime != null && s.StartTime >= utcStart && s.StartTime < utcEnd)
            .Select(s => s.DurationMinutes)
            .ToListAsync(cancellationToken);

        var actualMinutes = todaySessions.Sum(d => Math.Max(0, d));
        var completedSessionsCount = todaySessions.Count;
        var remainingMinutes = Math.Max(0, targetMinutes - actualMinutes);
        var progressPercentage = targetMinutes > 0
            ? Math.Min(100.0, Math.Round((double)actualMinutes / targetMinutes * 100.0, 1))
            : 100.0;
        var isAchieved = actualMinutes >= targetMinutes;

        var dailyGoalDto = new DailyGoalProgressDto
        {
            Date = targetDate.ToString("yyyy-MM-dd"),
            TargetMinutes = targetMinutes,
            ActualMinutes = actualMinutes,
            RemainingMinutes = remainingMinutes,
            ProgressPercentage = progressPercentage,
            IsAchieved = isAchieved,
            IsCustomTarget = isCustomTarget,
            CompletedSessionsCount = completedSessionsCount
        };

        // 3. Active Study Session
        var activeSession = await _context.StudySessions
            .Include(s => s.Subject)
            .AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsCompleted)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(cancellationToken);

        var activeSessionDto = activeSession?.ToDto(nowUtc);

        // 4. Tasks Summary
        var allTasks = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var totalTasks = allTasks.Count;
        var completedTasks = allTasks.Count(t => t.IsCompleted);
        var pendingTasks = totalTasks - completedTasks;
        var taskCompletionRate = totalTasks > 0
            ? Math.Round((double)completedTasks / totalTasks * 100.0, 1)
            : 0.0;

        var topPendingTasks = allTasks
            .Where(t => !t.IsCompleted)
            .Take(3)
            .Select(t => new DailyTaskDto(t.Id, t.Title, t.Description, t.IsCompleted, t.CreatedAt))
            .ToList();

        var tasksSummaryDto = new DashboardTaskSummaryDto
        {
            TotalCount = totalTasks,
            CompletedCount = completedTasks,
            PendingCount = pendingTasks,
            CompletionPercentage = taskCompletionRate,
            TopPendingTasks = topPendingTasks
        };

        // 5. Habits & Streaks Summary
        var habits = await _context.Habits
            .Include(h => h.Completions)
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        var habitDtos = habits.Select(h => HabitStreakCalculator.ToDto(h, targetDate)).ToList();
        var totalHabits = habitDtos.Count;
        var completedHabitsToday = habitDtos.Count(h => h.IsCompletedToday);
        var habitCompletionRate = totalHabits > 0
            ? Math.Round((double)completedHabitsToday / totalHabits * 100.0, 1)
            : 0.0;

        var habitsSummaryDto = new DashboardHabitSummaryDto
        {
            TotalCount = totalHabits,
            CompletedTodayCount = completedHabitsToday,
            CompletionPercentage = habitCompletionRate,
            Habits = habitDtos
        };

        // Calculate unified streak across habit completions
        var allCompletions = habits.SelectMany(h => h.Completions).ToList();
        var (isStreakActiveToday, currentStreak) = HabitStreakCalculator.CalculateStreak(allCompletions, targetDate);

        var uniqueCompletionDates = allCompletions
            .Select(c => c.Date.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        int longestStreak = 0;
        int runningStreak = 0;
        DateTime? previousDate = null;

        foreach (var d in uniqueCompletionDates)
        {
            if (previousDate.HasValue && d == previousDate.Value.AddDays(1))
            {
                runningStreak++;
            }
            else
            {
                runningStreak = 1;
            }

            if (runningStreak > longestStreak)
            {
                longestStreak = runningStreak;
            }

            previousDate = d;
        }

        if (currentStreak > longestStreak)
        {
            longestStreak = currentStreak;
        }

        var streakDto = new DashboardStreakSummaryDto
        {
            CurrentStreak = currentStreak,
            IsActiveToday = isStreakActiveToday,
            LongestStreak = longestStreak
        };

        // 6. Subjects Overview
        var subjects = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                ColorHex = s.ColorHex,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        // 7. Recent Completed Study Sessions
        var recentSessions = await _context.StudySessions
            .Include(s => s.Subject)
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsCompleted && s.EndTime != null)
            .OrderByDescending(s => s.EndTime)
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentSessionDtos = recentSessions.Select(s => s.ToDto(nowUtc)).ToList();

        // 8. Weekly Study Activity (7-Day Rolling Window)
        var weekStartDate = targetDate.AddDays(-6);
        var weekUtcStart = weekStartDate.AddMinutes(-offset);
        var weekUtcEnd = targetDate.AddDays(1).AddMinutes(-offset);

        var weekSessions = await _context.StudySessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsCompleted && s.EndTime != null && s.StartTime >= weekUtcStart && s.StartTime < weekUtcEnd)
            .Select(s => new { s.StartTime, s.DurationMinutes })
            .ToListAsync(cancellationToken);

        var weeklyActivity = new List<DashboardDayActivityDto>();
        for (int i = 6; i >= 0; i--)
        {
            var dayDate = targetDate.AddDays(-i);
            var dayUtcStart = dayDate.AddMinutes(-offset);
            var dayUtcEnd = dayUtcStart.AddDays(1);

            var daySessions = weekSessions
                .Where(s => s.StartTime >= dayUtcStart && s.StartTime < dayUtcEnd)
                .ToList();

            weeklyActivity.Add(new DashboardDayActivityDto
            {
                Date = dayDate.ToString("yyyy-MM-dd"),
                DayLabel = dayDate.ToString("ddd", CultureInfo.InvariantCulture),
                FocusMinutes = daySessions.Sum(s => Math.Max(0, s.DurationMinutes)),
                CompletedSessionsCount = daySessions.Count
            });
        }

        return new DashboardSummaryDto
        {
            DailyGoal = dailyGoalDto,
            ActiveSession = activeSessionDto,
            Tasks = tasksSummaryDto,
            Habits = habitsSummaryDto,
            Gamification = gamificationDto,
            Streak = streakDto,
            Subjects = subjects,
            RecentSessions = recentSessionDtos,
            WeeklyActivity = weeklyActivity
        };
    }
}
