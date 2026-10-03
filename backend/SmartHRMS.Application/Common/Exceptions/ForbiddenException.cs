namespace smartHRMS.Application.Common.Exceptions;

/// <summary>The user is signed in but not allowed to do this (403).</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
