using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MOT.Application.Common.Interfaces;
using MOT.Domain.Entities;

namespace MOT.Application.Habits.Commands
{
    public class CreateHabitCommand : IRequest<Guid>
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class CreateHabitCommandHandler : IRequestHandler<CreateHabitCommand, Guid>
    {
        private readonly IAppDbContext _context;

        public CreateHabitCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreateHabitCommand request, CancellationToken cancellationToken)
        {
            var habit = new Habit
            {
                UserId = request.UserId,
                Title = request.Title,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };

            _context.Habits.Add(habit);
            await _context.SaveChangesAsync(cancellationToken);

            return habit.Id;
        }
    }
}
