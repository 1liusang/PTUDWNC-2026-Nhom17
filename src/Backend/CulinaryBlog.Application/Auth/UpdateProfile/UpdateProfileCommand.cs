using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.UpdateProfile;

public record UpdateProfileCommand(string UserId, string? FullName, string? AvatarUrl) : IRequest<UserProfileDto>;
