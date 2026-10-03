namespace smartHRMS.Application.Common.Exceptions;

/// <summary>Sign-in failed or there is no valid session (401).</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
