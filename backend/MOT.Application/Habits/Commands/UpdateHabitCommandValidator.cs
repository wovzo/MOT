using FluentValidation;

namespace MOT.Application.Habits.Commands
{
    public class UpdateHabitCommandValidator : AbstractValidator<UpdateHabitCommand>
    {
        public UpdateHabitCommandValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Habit ID is required.");

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.");
        }
    }
}
