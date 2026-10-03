using System;

namespace MOT.Application.Common.Exceptions
{
    public class HabitNotFoundException : Exception
    {
        public HabitNotFoundException(Guid habitId)
            : base($"Habit with ID '{habitId}' was not found.")
        {
        }
    }
}
