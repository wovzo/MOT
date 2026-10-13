using FluentValidation;
using MOT.Application.Gamification.Queries;

namespace MOT.Application.Validators;

public class GetXpHistoryQueryValidator : AbstractValidator<GetXpHistoryQuery>
{
    public GetXpHistoryQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit must be between 1 and 100.");
    }
}
