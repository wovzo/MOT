using FluentValidation;
using MOT.Application.Analytics.Queries;

namespace MOT.Application.Validators
{
    public class GetWeeklyAnalyticsQueryValidator : AbstractValidator<GetWeeklyAnalyticsQuery>
    {
        public GetWeeklyAnalyticsQueryValidator()
        {
            RuleFor(x => x.Weeks)
                .InclusiveBetween(1, 12)
                .WithMessage("Weeks must be between 1 and 12.");
        }
    }
}
