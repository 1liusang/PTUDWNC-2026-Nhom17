namespace CulinaryBlog.Application.Auth.Dtos;

public record UserProfileDto(
    string Id,
    string FullName,
    string Email,
    string UserName,
    string? AvatarUrl,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt);
