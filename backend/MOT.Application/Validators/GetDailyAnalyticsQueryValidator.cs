using System;
using FluentValidation;
using MOT.Application.Analytics.Queries;

namespace MOT.Application.Validators
{
    public class GetDailyAnalyticsQueryValidator : AbstractValidator<GetDailyAnalyticsQuery>
    {
        public GetDailyAnalyticsQueryValidator()
        {
            RuleFor(x => x)
                .Must(x =>
                {
                    if (x.StartDate.HasValue && x.EndDate.HasValue)
                    {
                        return x.StartDate.Value.Date <= x.EndDate.Value.Date;
                    }
                    return true;
                })
                .WithMessage("StartDate cannot be after EndDate.")
                .Must(x =>
                {
                    if (x.StartDate.HasValue && x.EndDate.HasValue)
                    {
                        return (x.EndDate.Value.Date - x.StartDate.Value.Date).TotalDays <= 90;
                    }
                    return true;
                })
                .WithMessage("Date range cannot exceed 90 days.");

            When(x => x.StartDate.HasValue, () =>
            {
                RuleFor(x => x.StartDate!.Value)
                    .GreaterThanOrEqualTo(DateTime.UtcNow.AddYears(-5))
                    .WithMessage("StartDate cannot be more than 5 years in the past.")
                    .LessThanOrEqualTo(DateTime.UtcNow.AddYears(1))
                    .WithMessage("StartDate cannot be more than 1 year in the future.");
            });

            When(x => x.EndDate.HasValue, () =>
            {
                RuleFor(x => x.EndDate!.Value)
                    .GreaterThanOrEqualTo(DateTime.UtcNow.AddYears(-5))
                    .WithMessage("EndDate cannot be more than 5 years in the past.")
                    .LessThanOrEqualTo(DateTime.UtcNow.AddYears(1))
                    .WithMessage("EndDate cannot be more than 1 year in the future.");
            });
        }
    }
}
