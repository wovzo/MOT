using FluentValidation;

namespace MOT.Application.StudySessions
{
    public class StartStudySessionCommandValidator : AbstractValidator<StartStudySessionCommand>
    {
        public StartStudySessionCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");
        }
    }
}
