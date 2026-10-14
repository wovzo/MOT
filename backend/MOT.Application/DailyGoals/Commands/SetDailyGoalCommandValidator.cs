using FluentValidation;

namespace MOT.Application.DailyGoals.Commands;

public class SetDailyGoalCommandValidator : AbstractValidator<SetDailyGoalCommand>
{
    public SetDailyGoalCommandValidator()
    {
        RuleFor(v => v.TargetMinutes)
            .InclusiveBetween(5, 1440)
            .WithMessage("TargetMinutes must be between 5 and 1440 minutes (24 hours).");

        RuleFor(v => v.Date)
            .Matches(@"^\d{4}-\d{2}-\d{2}$")
            .When(v => !string.IsNullOrEmpty(v.Date))
            .WithMessage("Date must be in yyyy-MM-dd format.");

        RuleFor(v => v.TimeZoneOffsetMinutes)
            .InclusiveBetween(-840, 840)
            .When(v => v.TimeZoneOffsetMinutes.HasValue)
            .WithMessage("TimeZoneOffsetMinutes must be between -840 and +840.");
    }
}
