using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Interfaces;
using MOT.Application.DTOs;

namespace MOT.Application.Habits.Queries
{
    public class GetHabitsQuery : IRequest<List<HabitDto>>
    {
        public Guid UserId { get; set; }
    }

    public class GetHabitsQueryHandler : IRequestHandler<GetHabitsQuery, List<HabitDto>>
    {
        private readonly IAppDbContext _context;

        public GetHabitsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<List<HabitDto>> Handle(GetHabitsQuery request, CancellationToken cancellationToken)
        {
            var habits = await _context.Habits
                .Where(h => h.UserId == request.UserId)
                .Include(h => h.Completions)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync(cancellationToken);

            var today = DateTime.UtcNow.Date;
            var result = new List<HabitDto>();

            foreach (var habit in habits)
            {
                var completionDates = habit.Completions
                    .Select(c => c.Date.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();

                bool isCompletedToday = completionDates.Contains(today);
                int streak = 0;
                var checkDate = today;

                // If not completed today, we start checking from yesterday to see the active streak
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
                        // Skip if date is in the future relative to checkDate
                        continue;
                    }
                    else
                    {
                        // Streak broken
                        break;
                    }
                }

                result.Add(new HabitDto
                {
                    Id = habit.Id,
                    Title = habit.Title,
                    Description = habit.Description,
                    IsCompletedToday = isCompletedToday,
                    CurrentStreak = streak
                });
            }

            return result;
        }
    }
}
