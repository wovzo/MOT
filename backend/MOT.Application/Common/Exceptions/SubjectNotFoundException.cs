using System;

namespace MOT.Application.Common.Exceptions;

public class SubjectNotFoundException : Exception
{
    public SubjectNotFoundException(Guid id)
        : base($"Subject with ID {id} was not found.")
    {
    }
}
