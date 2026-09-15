using CulinaryBlog.Application.Auth.Dtos;
using MediatR;

namespace CulinaryBlog.Application.Auth.RefreshTokens;

public record RefreshTokenCommand(string RefreshToken, string? IpAddress) : IRequest<AuthResponseDto>;
