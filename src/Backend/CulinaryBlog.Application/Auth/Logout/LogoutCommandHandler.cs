using CulinaryBlog.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Auth.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public LogoutCommandHandler(IApplicationDbContext dbContext, IJwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _jwtService.HashRefreshToken(request.RefreshToken);

        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.UserId == request.CurrentUserId, cancellationToken);

        // Idempotent: không tìm thấy token vẫn coi là logout thành công, không tiết lộ trạng thái.
        if (token is not null && !token.IsRevoked)
        {
            token.Revoke();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
