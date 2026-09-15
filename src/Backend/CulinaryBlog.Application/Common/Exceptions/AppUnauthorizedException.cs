namespace CulinaryBlog.Application.Common.Exceptions;

/// <summary>Named to avoid clashing with System.UnauthorizedAccessException semantics; maps to HTTP 401.</summary>
public class AppUnauthorizedException : Exception
{
    public AppUnauthorizedException(string message) : base(message)
    {
    }
}
