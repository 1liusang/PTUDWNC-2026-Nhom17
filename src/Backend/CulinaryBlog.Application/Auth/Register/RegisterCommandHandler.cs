using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Auth.Mappers;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Domain.Entities;
using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Application.Auth.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public RegisterCommandHandler(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext dbContext,
        IJwtService jwtService,
        IBackgroundJobClient backgroundJobClient)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _jwtService = jwtService;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new ConflictException("Email đã được đăng ký.");
        }

        var user = ApplicationUser.Create(request.FullName, request.Email, request.UserName);

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw new FluentValidation.ValidationException(createResult.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(nameof(request.Password), e.Description)));
        }

        await _userManager.AddToRoleAsync(user, Roles.Author);

        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _jwtService.GenerateAccessToken(user, roles);
        var rawRefreshToken = _jwtService.GenerateRefreshTokenValue();
        var refreshTokenHash = _jwtService.HashRefreshToken(rawRefreshToken);

        var refreshToken = RefreshToken.Create(user.Id, refreshTokenHash, RefreshTokenLifetime, request.IpAddress);
        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _backgroundJobClient.Enqueue<IWelcomeEmailJob>(job => job.ExecuteAsync(user.Id, CancellationToken.None));

        return new AuthResponseDto(
            accessToken.Token,
            rawRefreshToken,
            accessToken.ExpiresAt,
            UserProfileMapper.ToDto(user, roles));
    }
}
