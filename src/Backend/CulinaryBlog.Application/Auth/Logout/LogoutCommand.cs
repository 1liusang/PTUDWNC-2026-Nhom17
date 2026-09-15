using MediatR;

namespace CulinaryBlog.Application.Auth.Logout;

public record LogoutCommand(string RefreshToken, string CurrentUserId) : IRequest;
