namespace CulinaryBlog.Application.Common.Exceptions;

public class AccountLockedException : Exception
{
    public DateTimeOffset? LockoutEnd { get; }

    public AccountLockedException(string message, DateTimeOffset? lockoutEnd) : base(message)
    {
        LockoutEnd = lockoutEnd;
    }
}
