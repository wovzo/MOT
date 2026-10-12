using FluentValidation;

namespace MOT.Application.Classrooms;

public class JoinRoomCommandValidator : AbstractValidator<JoinRoomCommand>
{
    public JoinRoomCommandValidator()
    {
        RuleFor(x => x.RoomName)
            .NotEmpty().WithMessage("Room name is required.")
            .MaximumLength(100).WithMessage("Room name cannot exceed 100 characters.")
            .Matches(@"^[a-zA-Z0-9_\-]+$").WithMessage("Room name can only contain alphanumeric characters, underscores, and hyphens.");
    }
}

public class PingRoomCommandValidator : AbstractValidator<PingRoomCommand>
{
    public PingRoomCommandValidator()
    {
        RuleFor(x => x.RoomName)
            .NotEmpty().WithMessage("Room name is required.")
            .MaximumLength(100).WithMessage("Room name cannot exceed 100 characters.");
    }
}

public class LeaveRoomCommandValidator : AbstractValidator<LeaveRoomCommand>
{
    public LeaveRoomCommandValidator()
    {
        RuleFor(x => x.RoomName)
            .NotEmpty().WithMessage("Room name is required.")
            .MaximumLength(100).WithMessage("Room name cannot exceed 100 characters.");
    }
}

public class GetRoomParticipantsQueryValidator : AbstractValidator<GetRoomParticipantsQuery>
{
    public GetRoomParticipantsQueryValidator()
    {
        RuleFor(x => x.RoomName)
            .NotEmpty().WithMessage("Room name is required.")
            .MaximumLength(100).WithMessage("Room name cannot exceed 100 characters.");
    }
}
