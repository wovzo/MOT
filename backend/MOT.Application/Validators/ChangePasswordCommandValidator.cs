using FluentValidation;
using MOT.Application.Users.Commands;

namespace MOT.Application.Validators;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required.")
            .MinimumLength(6)
            .WithMessage("New password must be at least 6 characters long.")
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("New password must be different from current password.");
    }
}
