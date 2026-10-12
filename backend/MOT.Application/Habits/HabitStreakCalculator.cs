using System;
using System.Collections.Generic;
using System.Linq;
using MOT.Application.DTOs;
using MOT.Domain.Entities;

namespace MOT.Application.Habits
{
    public static class HabitStreakCalculator
    {
        public static (bool IsCompletedToday, int CurrentStreak) CalculateStreak(IEnumerable<HabitCompletion>? completions, DateTime? todayOverride = null)
        {
            var today = todayOverride?.Date ?? DateTime.UtcNow.Date;

            if (completions == null)
            {
                return (false, 0);
            }

            var completionDates = completions
                .Select(c => c.Date.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            bool isCompletedToday = completionDates.Contains(today);
            int streak = 0;
            var checkDate = today;

            if (!isCompletedToday)
            {
                checkDate = today.AddDays(-1);
            }

            foreach (var date in completionDates)
            {
                if (date == checkDate)
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }
                else if (date > checkDate)
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            return (isCompletedToday, streak);
        }

        public static HabitDto ToDto(Habit habit, DateTime? todayOverride = null)
        {
            var (isCompletedToday, streak) = CalculateStreak(habit.Completions, todayOverride);
            return new HabitDto
            {
                Id = habit.Id,
                Title = habit.Title,
                Description = habit.Description,
                IsCompletedToday = isCompletedToday,
                CurrentStreak = streak
            };
        }
    }
}
