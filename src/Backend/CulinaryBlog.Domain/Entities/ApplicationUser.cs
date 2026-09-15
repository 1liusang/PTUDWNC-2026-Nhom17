using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; private set; } = null!;
    public string? AvatarUrl { get; private set; }
    public string? Bio { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private ApplicationUser()
    {
    }

    public static ApplicationUser Create(string displayName, string email, string userName)
    {
        return new ApplicationUser
        {
            DisplayName = displayName,
            Email = email,
            UserName = userName,
        };
    }

    public void UpdateProfile(string? displayName, string? avatarUrl)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName;
        }

        if (avatarUrl is not null)
        {
            AvatarUrl = avatarUrl;
        }
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
