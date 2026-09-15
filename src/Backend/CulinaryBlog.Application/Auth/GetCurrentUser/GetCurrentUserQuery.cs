using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.GetCurrentUser;

public record GetCurrentUserQuery(string UserId) : IRequest<UserProfileDto>;
