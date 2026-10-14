using FluentValidation;

namespace MOT.Application.Subjects.Commands;

public class UpdateSubjectCommandValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Subject ID is required.");

        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Subject name is required.")
            .MaximumLength(100).WithMessage("Subject name must not exceed 100 characters.");

        RuleFor(v => v.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(v => v.ColorHex)
            .MaximumLength(9).WithMessage("ColorHex must not exceed 9 characters.")
            .Matches(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$")
            .When(v => !string.IsNullOrEmpty(v.ColorHex))
            .WithMessage("ColorHex must be a valid hex color code (e.g. #6C5CE7).");
    }
}
