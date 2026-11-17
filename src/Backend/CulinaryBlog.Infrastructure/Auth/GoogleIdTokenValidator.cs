using System.Text.Json;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Auth;
using CulinaryBlog.Application.Features.Auth.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.Infrastructure.Auth;

/// <summary>
/// Xác minh id_token của Google (S-05): chữ ký RS256 bằng JWKS của Google, issuer, audience = Client ID, hạn dùng.
/// </summary>
public sealed class GoogleIdTokenValidator(
    IOptions<GoogleAuthOptions> options,
    IGoogleSigningKeySource signingKeys,
    TimeProvider time,
    ILogger<GoogleIdTokenValidator> logger) : IGoogleIdTokenValidator
{
    private static readonly string[] Issuers = ["accounts.google.com", "https://accounts.google.com"];
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private readonly JsonWebTokenHandler _handler = new();

    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            logger.LogWarning("Google sign-in was requested but Authentication:Google:ClientId is not configured");
            throw Unavailable();
        }

        // Kiểm tra rẻ trước khi cần tới mạng: token giả/sai định dạng/sai audience bị từ chối ngay.
        if (!TryRead(idToken, out var unverified)
            || unverified.Alg != SecurityAlgorithms.RsaSha256
            || string.IsNullOrEmpty(unverified.Kid)
            || !unverified.Audiences.Contains(clientId)
            || !Issuers.Contains(unverified.Issuer))
        {
            throw Invalid();
        }

        var result = await ValidateSignatureAsync(idToken, clientId, forceRefresh: false, cancellationToken);
        if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // Google xoay khóa định kỳ: kid mới chưa có trong cache thì tải lại một lần.
            result = await ValidateSignatureAsync(idToken, clientId, forceRefresh: true, cancellationToken);
        }

        if (!result.IsValid || result.SecurityToken is not JsonWebToken token)
        {
            throw Invalid();
        }

        var email = token.TryGetPayloadValue<string>("email", out var value) ? value : null;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw Invalid();
        }

        return new GoogleIdentity(
            token.Subject,
            email,
            IsEmailVerified(token),
            token.TryGetPayloadValue<string>("name", out var name) ? name : null,
            token.TryGetPayloadValue<string>("picture", out var picture) ? picture : null);
    }

    private async Task<TokenValidationResult> ValidateSignatureAsync(
        string idToken,
        string clientId,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<SecurityKey> keys;
        try
        {
            keys = await signingKeys.GetKeysAsync(forceRefresh, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or ArgumentException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "Could not fetch Google signing keys from {Url}", GoogleSigningKeys.JwksUrl);
            throw Unavailable();
        }

        return await _handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = Issuers,
            ValidAudience = clientId,
            IssuerSigningKeys = keys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ClockSkew = ClockSkew,
            LifetimeValidator = (notBefore, expires, _, _) => IsWithinLifetime(notBefore, expires),
        });
    }

    private bool TryRead(string idToken, out JsonWebToken token)
    {
        token = null!;
        if (!_handler.CanReadToken(idToken))
        {
            return false;
        }

        try
        {
            token = _handler.ReadJsonWebToken(idToken);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenMalformedException)
        {
            return false;
        }
    }

    private bool IsWithinLifetime(DateTime? notBefore, DateTime? expires)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return expires is { } exp && now <= exp + ClockSkew && (notBefore is not { } nbf || now >= nbf - ClockSkew);
    }

    // Google trả email_verified dạng boolean; một số token cũ trả chuỗi "true".
    private static bool IsEmailVerified(JsonWebToken token) =>
        (token.TryGetPayloadValue<bool>("email_verified", out var verified) && verified)
        || (token.TryGetPayloadValue<string>("email_verified", out var text)
            && string.Equals(text, "true", StringComparison.OrdinalIgnoreCase));

    private static UnauthorizedException Invalid() =>
        new("The Google token is invalid.", AuthErrorCodes.GoogleTokenInvalid);

    private static ExternalServiceException Unavailable() =>
        new(AuthErrorCodes.GoogleUnavailable, "Google sign-in is currently unavailable.");
}
