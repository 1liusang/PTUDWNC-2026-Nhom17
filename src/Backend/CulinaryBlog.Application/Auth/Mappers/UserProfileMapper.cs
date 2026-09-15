using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Auth.Mappers;

public static class UserProfileMapper
{
    public static UserProfileDto ToDto(ApplicationUser user, IList<string> roles) => new(
        user.Id,
        user.DisplayName,
        user.Email!,
        user.UserName!,
        user.AvatarUrl,
        roles.ToList(),
        user.EmailConfirmed,
        user.CreatedAt);
}
