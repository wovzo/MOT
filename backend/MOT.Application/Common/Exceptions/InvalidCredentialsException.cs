namespace MOT.Application.Common.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid email or password.")
    {
    }

    public InvalidCredentialsException(string message)
        : base(message)
    {
    }
}
