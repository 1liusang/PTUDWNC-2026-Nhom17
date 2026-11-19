using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Auth;
using CulinaryBlog.Application.Features.Auth.Abstractions;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.UnitTests.Features.Auth.Fakes;

namespace CulinaryBlog.UnitTests.Features.Auth;

public sealed class GoogleLoginCommandHandlerTests
{
    private const string Picture = "https://example.com/avatar.png";

    private readonly AuthTestContext _context = new();

    [Fact]
    public async Task Handle_NewGoogleUser_CreatesAccountWithAvatarAndLinksLogin()
    {
        var identity = new GoogleIdentity("google-sub", "new@example.com", EmailVerified: true, "Ngọc Tuấn", Picture);

        var response = await HandleAsync(identity);

        Assert.Equal("Ngọc Tuấn", response.User.FullName);
        Assert.Equal("new", response.User.UserName);
        Assert.Equal(Picture, response.User.AvatarUrl);
        Assert.True(_context.Users.HasLogin(new ExternalLogin(GoogleLoginCommandHandler.ProviderName, "google-sub")));
        Assert.Single(_context.Jobs.Enqueued);
    }

    [Fact]
    public async Task Handle_ExistingLocalAccount_LinksInsteadOfCreating()
    {
        var existing = _context.Users.Add("local@example.com", "Author@12345");
        var identity = new GoogleIdentity("google-sub", "local@example.com", EmailVerified: true, null, Picture);

        var response = await HandleAsync(identity);

        Assert.Equal(existing.Id, response.User.Id);
        Assert.Equal(Picture, response.User.AvatarUrl);
        Assert.Empty(_context.Jobs.Enqueued);
    }

    [Fact]
    public async Task Handle_UnverifiedEmail_ThrowsWithoutLinking()
    {
        _context.Users.Add("local@example.com", "Author@12345");
        var identity = new GoogleIdentity("google-sub", "local@example.com", EmailVerified: false, null, null);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => HandleAsync(identity));

        Assert.Equal(AuthErrorCodes.GoogleEmailUnverified, exception.Code);
        Assert.False(_context.Users.HasLogin(new ExternalLogin(GoogleLoginCommandHandler.ProviderName, "google-sub")));
    }

    [Fact]
    public async Task Handle_ShortGoogleName_FallsBackToEmailPrefix()
    {
        var identity = new GoogleIdentity("google-sub", "tuan@example.com", EmailVerified: true, "T", null);

        var response = await HandleAsync(identity);

        Assert.Equal("tuan", response.User.FullName);
    }

    [Fact]
    public async Task Handle_UserNameTaken_AppendsNumericSuffix()
    {
        await HandleAsync(new GoogleIdentity("sub-1", "tuan@example.com", EmailVerified: true, "Ngọc Tuấn", null));

        var response = await HandleAsync(new GoogleIdentity("sub-2", "tuan@other.com", EmailVerified: true, "Tuấn Khác", null));

        Assert.Matches("^tuan[0-9]{4,5}$", response.User.UserName);
    }

    [Fact]
    public async Task Handle_NonAsciiOrLongEmailPrefix_GeneratesValidUserName()
    {
        var longLocal = new string('a', 80);
        var response = await HandleAsync(new GoogleIdentity("sub-1", $"{longLocal}+tag@example.com", EmailVerified: true, "Ngọc Tuấn", null));

        Assert.Matches("^[a-zA-Z0-9]{3,45}$", response.User.UserName);
    }

    [Fact]
    public async Task Handle_ReturningGoogleUser_SignsInWithoutCreatingAgain()
    {
        var identity = new GoogleIdentity("google-sub", "back@example.com", EmailVerified: true, "Ngọc Tuấn", null);
        var first = await HandleAsync(identity);

        var second = await HandleAsync(identity);

        Assert.Equal(first.User.Id, second.User.Id);
        Assert.Single(_context.Jobs.Enqueued);
    }

    private Task<Application.Features.Auth.Common.AuthResponse> HandleAsync(GoogleIdentity identity)
    {
        var handler = new GoogleLoginCommandHandler(new StubGoogleValidator(identity), _context.Users, _context.Sessions, _context.Jobs, _context.Audit);
        return handler.Handle(new GoogleLoginCommand("id-token"), CancellationToken.None);
    }

    private sealed class StubGoogleValidator(GoogleIdentity identity) : IGoogleIdTokenValidator
    {
        public Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken) =>
            Task.FromResult(identity);
    }
}
