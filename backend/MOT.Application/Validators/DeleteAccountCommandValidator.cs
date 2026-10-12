using FluentValidation;
using MOT.Application.Users.Commands;

namespace MOT.Application.Validators;

public class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required to confirm account deletion.");
    }
}
