using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.DTOs;

namespace MOT.Application.Habits.Queries
{
    public class GetHabitByIdQuery : IRequest<HabitDto>
    {
        public Guid Id { get; set; }

        public GetHabitByIdQuery() { }

        public GetHabitByIdQuery(Guid id)
        {
            Id = id;
        }
    }

    public class GetHabitByIdQueryHandler : IRequestHandler<GetHabitByIdQuery, HabitDto>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public GetHabitByIdQueryHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<HabitDto> Handle(GetHabitByIdQuery request, CancellationToken cancellationToken)
        {
            var userIdStr = _currentUserService.UserId ?? throw new UnauthorizedAccessException();
            if (!Guid.TryParse(userIdStr, out var userId))
                throw new UnauthorizedAccessException();

            var habit = await _context.Habits
                .Include(h => h.Completions)
                .FirstOrDefaultAsync(h => h.Id == request.Id && h.UserId == userId, cancellationToken);

            if (habit == null)
            {
                throw new HabitNotFoundException(request.Id);
            }

            return HabitStreakCalculator.ToDto(habit);
        }
    }
}
