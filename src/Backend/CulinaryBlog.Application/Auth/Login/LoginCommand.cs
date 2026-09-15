using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.Login;

public record LoginCommand(string Email, string Password, string? IpAddress) : IRequest<AuthResponseDto>;
