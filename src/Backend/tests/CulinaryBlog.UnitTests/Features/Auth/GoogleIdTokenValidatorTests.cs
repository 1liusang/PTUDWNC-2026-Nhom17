using System.Security.Cryptography;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Auth;
using CulinaryBlog.Infrastructure.Auth;
using CulinaryBlog.UnitTests.Features.Auth.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.UnitTests.Features.Auth;

public sealed class GoogleIdTokenValidatorTests
{
    private const string ClientId = "test-client.apps.googleusercontent.com";

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _time = new(Now);
    private readonly RsaSecurityKey _googleKey = NewKey("google-key-1");
    private readonly StubKeySource _keys;

    public GoogleIdTokenValidatorTests()
    {
        _keys = new StubKeySource(_googleKey);
    }

    [Fact]
    public async Task ValidateAsync_ValidToken_ReturnsIdentity()
    {
        var identity = await Validator().ValidateAsync(Sign(_googleKey), CancellationToken.None);

        Assert.Equal("google-sub", identity.Subject);
        Assert.Equal("tuan@example.com", identity.Email);
        Assert.True(identity.EmailVerified);
        Assert.Equal("Ngọc Tuấn", identity.Name);
        Assert.Equal("https://example.com/a.png", identity.PictureUrl);
    }

    [Fact]
    public async Task ValidateAsync_EmailVerifiedAsString_IsAccepted()
    {
        var identity = await Validator().ValidateAsync(
            Sign(_googleKey, claims => claims["email_verified"] = "true"),
            CancellationToken.None);

        Assert.True(identity.EmailVerified);
    }

    [Fact]
    public async Task ValidateAsync_WrongAudience_ThrowsWithoutFetchingKeys()
    {
        await AssertInvalidAsync(Sign(_googleKey, audience: "other-client"));

        Assert.Equal(0, _keys.Calls);
    }

    [Fact]
    public async Task ValidateAsync_WrongIssuer_Throws() =>
        await AssertInvalidAsync(Sign(_googleKey, issuer: "https://evil.example.com"));

    [Fact]
    public async Task ValidateAsync_Expired_Throws()
    {
        var token = Sign(_googleKey);
        _time.Advance(TimeSpan.FromHours(2));

        await AssertInvalidAsync(token);
    }

    [Fact]
    public async Task ValidateAsync_SignedByAnotherKeyWithSameKid_Throws() =>
        await AssertInvalidAsync(Sign(NewKey(_googleKey.KeyId!)));

    [Fact]
    public async Task ValidateAsync_UnknownKid_RefreshesKeysOnce()
    {
        var rotated = NewKey("google-key-2");
        _keys.AfterRefresh = rotated;

        var identity = await Validator().ValidateAsync(Sign(rotated), CancellationToken.None);

        Assert.Equal("google-sub", identity.Subject);
        Assert.Equal(1, _keys.ForcedRefreshes);
    }

    [Theory]
    [InlineData("fake")]
    [InlineData("not.a.jwt")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0.")]
    public async Task ValidateAsync_MalformedOrUnsigned_Throws(string idToken)
    {
        await AssertInvalidAsync(idToken);

        Assert.Equal(0, _keys.Calls);
    }

    [Fact]
    public async Task ValidateAsync_KeysUnreachable_ThrowsUnavailable()
    {
        _keys.Failure = new HttpRequestException("network down");

        var exception = await Assert.ThrowsAsync<ExternalServiceException>(
            () => Validator().ValidateAsync(Sign(_googleKey), CancellationToken.None));

        Assert.Equal(AuthErrorCodes.GoogleUnavailable, exception.Code);
    }

    private GoogleIdTokenValidator Validator() =>
        new(Options.Create(new GoogleAuthOptions { ClientId = ClientId }), _keys, _time, NullLogger<GoogleIdTokenValidator>.Instance);

    private async Task AssertInvalidAsync(string idToken)
    {
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => Validator().ValidateAsync(idToken, CancellationToken.None));
        Assert.Equal(AuthErrorCodes.GoogleTokenInvalid, exception.Code);
    }

    private string Sign(
        RsaSecurityKey key,
        Action<Dictionary<string, object>>? customize = null,
        string audience = ClientId,
        string issuer = "https://accounts.google.com")
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "google-sub",
            ["email"] = "tuan@example.com",
            ["email_verified"] = true,
            ["name"] = "Ngọc Tuấn",
            ["picture"] = "https://example.com/a.png",
        };
        customize?.Invoke(claims);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = Now.UtcDateTime,
            NotBefore = Now.UtcDateTime,
            Expires = Now.UtcDateTime.AddHours(1),
            Claims = claims,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });
    }

    private static RsaSecurityKey NewKey(string kid) => new(RSA.Create(2048)) { KeyId = kid };

    private sealed class StubKeySource(SecurityKey initial) : IGoogleSigningKeySource
    {
        public int Calls { get; private set; }

        public int ForcedRefreshes { get; private set; }

        public SecurityKey? AfterRefresh { get; set; }

        public Exception? Failure { get; set; }

        public Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            Calls++;
            if (Failure is not null)
            {
                throw Failure;
            }

            if (forceRefresh)
            {
                ForcedRefreshes++;
                if (AfterRefresh is not null)
                {
                    return Task.FromResult<IReadOnlyCollection<SecurityKey>>([initial, AfterRefresh]);
                }
            }

            return Task.FromResult<IReadOnlyCollection<SecurityKey>>([initial]);
        }
    }
}
