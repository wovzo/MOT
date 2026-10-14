using FluentValidation;

namespace MOT.Application.StudySessions;

public class ResumeStudySessionCommandValidator : AbstractValidator<ResumeStudySessionCommand>
{
    public ResumeStudySessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID must not be empty.");
    }
}
