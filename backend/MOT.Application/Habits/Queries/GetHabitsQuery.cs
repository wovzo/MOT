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
    }

    public class GetHabitsQueryHandler : IRequestHandler<GetHabitsQuery, List<HabitDto>>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetHabitsQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<HabitDto>> Handle(GetHabitsQuery request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var habits = await _context.Habits
                .Where(h => h.UserId == userId)
                .Include(h => h.Completions)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync(cancellationToken);

            return habits.Select(h => HabitStreakCalculator.ToDto(h)).ToList();
        }
    }
}
