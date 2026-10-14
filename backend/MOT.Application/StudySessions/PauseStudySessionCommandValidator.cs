using FluentValidation;

namespace MOT.Application.StudySessions;

public class PauseStudySessionCommandValidator : AbstractValidator<PauseStudySessionCommand>
{
    public PauseStudySessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID must not be empty.");
    }
}
