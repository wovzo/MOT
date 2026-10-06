using System;

namespace MOT.Domain.Entities
{
    public class RoomParticipant
    {
        public Guid Id { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public bool IsVideoOn { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime LastPing { get; set; }
    }
}
