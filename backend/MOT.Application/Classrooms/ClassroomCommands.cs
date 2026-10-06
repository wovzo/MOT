using System;
using System.Collections.Generic;
using MediatR;
using MOT.Domain.Entities;

namespace MOT.Application.Classrooms
{
    public class RoomParticipantDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public bool IsVideoOn { get; set; }
    }

    public class JoinRoomCommand : IRequest<RoomParticipantDto>
    {
        public string RoomName { get; set; } = string.Empty;
        public bool IsVideoOn { get; set; }
    }

    public class PingRoomCommand : IRequest<Unit>
    {
        public string RoomName { get; set; } = string.Empty;
    }

    public class LeaveRoomCommand : IRequest<Unit>
    {
        public string RoomName { get; set; } = string.Empty;
    }

    public class GetRoomParticipantsQuery : IRequest<List<RoomParticipantDto>>
    {
        public string RoomName { get; set; } = string.Empty;
    }
}
