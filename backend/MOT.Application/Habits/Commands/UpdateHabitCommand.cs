using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;
using MOT.Application.DTOs;

namespace MOT.Application.Habits.Commands
{
    public class UpdateHabitCommand : IRequest<HabitDto>
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateHabitCommandHandler : IRequestHandler<UpdateHabitCommand, HabitDto>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public UpdateHabitCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<HabitDto> Handle(UpdateHabitCommand request, CancellationToken cancellationToken)
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

            habit.Title = request.Title;
            habit.Description = request.Description;

            // Historical completions and CreatedAt are left completely untouched.
            await _context.SaveChangesAsync(cancellationToken);

            return HabitStreakCalculator.ToDto(habit);
        }
    }
}
