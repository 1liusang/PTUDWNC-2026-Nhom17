using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Auth.Mappers;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private const string GenericInvalidCredentialsMessage = "Email hoặc mật khẩu không đúng.";
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext dbContext,
        IJwtService jwtService)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Không tiết lộ tài khoản có tồn tại hay không (chống User Enumeration Attack).
            throw new AppUnauthorizedException(GenericInvalidCredentialsMessage);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            throw new AccountLockedException("Tài khoản đang bị khóa tạm thời.", lockoutEnd);
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);

            if (await _userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                throw new AccountLockedException("Tài khoản đã bị khóa do đăng nhập sai quá số lần cho phép.", lockoutEnd);
            }

            throw new AppUnauthorizedException(GenericInvalidCredentialsMessage);
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _jwtService.GenerateAccessToken(user, roles);
        var rawRefreshToken = _jwtService.GenerateRefreshTokenValue();
        var refreshTokenHash = _jwtService.HashRefreshToken(rawRefreshToken);

        var refreshToken = RefreshToken.Create(user.Id, refreshTokenHash, RefreshTokenLifetime, request.IpAddress);
        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto(
            accessToken.Token,
            rawRefreshToken,
            accessToken.ExpiresAt,
            UserProfileMapper.ToDto(user, roles));
    }
}
