using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.Register;

public record RegisterCommand(
    string FullName,
    string Email,
    string UserName,
    string Password,
    string? IpAddress) : IRequest<AuthResponseDto>;
