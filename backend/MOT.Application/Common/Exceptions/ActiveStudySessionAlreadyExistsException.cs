using System;

namespace MOT.Application.Common.Exceptions
{
    public class ActiveStudySessionAlreadyExistsException : Exception
    {
        public ActiveStudySessionAlreadyExistsException()
            : base("You already have an active study session.")
        {
        }

        public ActiveStudySessionAlreadyExistsException(string message)
            : base(message)
        {
        }
    }
}
