using System;

namespace MOT.Application.Common.Exceptions;

public class ClassroomFullException : Exception
{
    public ClassroomFullException(string roomName)
        : base($"Classroom '{roomName}' is full. Maximum capacity is 50 participants.")
    {
    }
}
