namespace CulinaryBlog.Application.Auth.Dtos;

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserProfileDto User);
