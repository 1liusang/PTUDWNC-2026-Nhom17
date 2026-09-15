using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Common.Interfaces;

public record GeneratedAccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtService
{
    GeneratedAccessToken GenerateAccessToken(ApplicationUser user, IList<string> roles);

    /// <summary>Returns the raw refresh token to send to the client. Caller is responsible for hashing before persisting.</summary>
    string GenerateRefreshTokenValue();

    string HashRefreshToken(string rawToken);
}
