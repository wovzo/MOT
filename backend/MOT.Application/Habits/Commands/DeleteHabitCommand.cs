using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MOT.Application.Common.Exceptions;
using MOT.Application.Common.Interfaces;

namespace MOT.Application.Habits.Commands
{
    public class DeleteHabitCommand : IRequest<Unit>
    {
        public Guid Id { get; set; }

        public DeleteHabitCommand() { }

        public DeleteHabitCommand(Guid id)
        {
            Id = id;
        }
    }

    public class DeleteHabitCommandHandler : IRequestHandler<DeleteHabitCommand, Unit>
    {
        private readonly IAppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public DeleteHabitCommandHandler(IAppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<Unit> Handle(DeleteHabitCommand request, CancellationToken cancellationToken)
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

            // The existing schema explicitly configures FK_HabitCompletions_Habits_HabitId with ON DELETE CASCADE.
            // Child completions are removed explicitly to ensure consistent behavior across both unit mocks and EF relational persistence.
            if (habit.Completions != null && habit.Completions.Count > 0)
            {
                _context.HabitCompletions.RemoveRange(habit.Completions);
            }

            _context.Habits.Remove(habit);
            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
