using FluentValidation;

namespace MOT.Application.Habits.Commands
{
    public class CreateHabitCommandValidator : AbstractValidator<CreateHabitCommand>
    {
        public CreateHabitCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");
        }
    }
}
