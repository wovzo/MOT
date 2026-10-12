using FluentValidation;
using MOT.Application.Users.Commands;

namespace MOT.Application.Validators;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Display name is required.")
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Display name cannot be empty or whitespace.")
            .MaximumLength(100)
            .WithMessage("Display name cannot exceed 100 characters.");
    }
}
