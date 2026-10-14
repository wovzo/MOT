using System;

namespace MOT.Application.Common.Exceptions;

public class DuplicateSubjectNameException : Exception
{
    public DuplicateSubjectNameException(string name)
        : base($"A subject with the name '{name}' already exists.")
    {
    }
}
