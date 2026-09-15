using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Auth.Mappers;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ApplicationUser = CulinaryBlog.Domain.Entities.ApplicationUser;
using RefreshTokenEntity = CulinaryBlog.Domain.Entities.RefreshToken;

namespace CulinaryBlog.Application.Auth.RefreshTokens;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    private const string InvalidTokenMessage = "Refresh token không hợp lệ hoặc đã hết hạn.";
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IJwtService jwtService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var incomingHash = _jwtService.HashRefreshToken(request.RefreshToken);

        var existingToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == incomingHash, cancellationToken);

        if (existingToken is null)
        {
            throw new AppUnauthorizedException(InvalidTokenMessage);
        }

        if (existingToken.IsRevoked)
        {
            // A2 — Refresh Token Reuse Attack detected: token đã bị revoke nhưng vẫn được dùng lại.
            _logger.LogWarning(
                "SECURITY ALERT: Refresh token reuse detected for user {UserId}. Revoking all active tokens for this user.",
                existingToken.UserId);

            var activeFamilyTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in activeFamilyTokens)
            {
                token.Revoke();
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            throw new AppUnauthorizedException(InvalidTokenMessage);
        }

        if (existingToken.IsExpired)
        {
            throw new AppUnauthorizedException(InvalidTokenMessage);
        }

        var user = existingToken.User;
        if (user is null || !user.IsActive)
        {
            throw new AppUnauthorizedException(InvalidTokenMessage);
        }

        var rawNewRefreshToken = _jwtService.GenerateRefreshTokenValue();
        var newRefreshTokenHash = _jwtService.HashRefreshToken(rawNewRefreshToken);

        existingToken.Revoke(newRefreshTokenHash);

        var newRefreshToken = RefreshTokenEntity.Create(user.Id, newRefreshTokenHash, RefreshTokenLifetime, request.IpAddress);
        _dbContext.RefreshTokens.Add(newRefreshToken);

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user, roles);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto(
            accessToken.Token,
            rawNewRefreshToken,
            accessToken.ExpiresAt,
            UserProfileMapper.ToDto(user, roles));
    }
}
