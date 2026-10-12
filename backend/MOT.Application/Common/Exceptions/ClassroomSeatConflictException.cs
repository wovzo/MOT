using System;

namespace MOT.Application.Common.Exceptions;

public class ClassroomSeatConflictException : Exception
{
    public ClassroomSeatConflictException(string roomName, int seatNumber)
        : base($"Seat {seatNumber} in classroom '{roomName}' is already occupied.")
    {
    }
}
