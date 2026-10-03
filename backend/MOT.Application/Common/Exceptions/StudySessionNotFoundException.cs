using System;

namespace MOT.Application.Common.Exceptions
{
    public class StudySessionNotFoundException : Exception
    {
        public StudySessionNotFoundException(Guid sessionId)
            : base($"Study session with ID '{sessionId}' was not found.")
        {
        }
    }
}
