using System;

namespace MOT.Application.Common.Exceptions;

public class InvalidStudySessionStateException : Exception
{
    public InvalidStudySessionStateException(string message) : base(message)
    {
    }
}
