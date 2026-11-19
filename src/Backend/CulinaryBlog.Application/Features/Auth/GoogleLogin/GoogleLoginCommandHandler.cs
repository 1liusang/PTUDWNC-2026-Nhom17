using CulinaryBlog.Application.Abstractions;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Auth.Abstractions;
using CulinaryBlog.Application.Features.Auth.Common;
using CulinaryBlog.Domain.Auth;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public sealed class GoogleLoginCommandHandler(
    IGoogleIdTokenValidator googleTokens,
    IUserAccountService users,
    AuthSessionIssuer sessions,
    IBackgroundJobService jobs,
    LoginAudit audit) : IRequestHandler<GoogleLoginCommand, AuthResponse>
{
    public const string ProviderName = "Google";

    private const int MaxUserNameAttempts = 5;

    // Chừa 5 ký tự cho hậu tố số để không vượt giới hạn độ dài username.
    private const int UserNameBaseMaxLength = AuthValidationRules.UserNameMaxLength - 5;

    public async Task<AuthResponse> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var identity = await googleTokens.ValidateAsync(request.IdToken, cancellationToken);

        // Chỉ liên kết với tài khoản có sẵn khi Google đã xác minh email, tránh chiếm tài khoản.
        if (!identity.EmailVerified)
        {
            throw new UnauthorizedException(
                "The Google account email is not verified.",
                AuthErrorCodes.GoogleEmailUnverified);
        }

        var login = new ExternalLogin(ProviderName, identity.Subject);
        var user = await users.FindByExternalLoginAsync(login, cancellationToken)
            ?? await LinkOrCreateAsync(identity, login, cancellationToken);

        if (!user.IsActive)
        {
            await audit.RecordAsync(user.Id, LoginMethod.Google, LoginResult.Disabled, cancellationToken);
            throw new ForbiddenException("The account is disabled.", AuthErrorCodes.AccountDisabled);
        }

        var response = await sessions.StartAsync(user, cancellationToken);
        await audit.RecordAsync(user.Id, LoginMethod.Google, LoginResult.Succeeded, cancellationToken);
        return response;
    }

    private async Task<UserAccount> LinkOrCreateAsync(
        GoogleIdentity identity,
        ExternalLogin login,
        CancellationToken cancellationToken)
    {
        var existing = await users.FindByEmailAsync(identity.Email, cancellationToken);
        if (existing is not null)
        {
            return await users.LinkExternalLoginAsync(existing.Id, login, identity.PictureUrl, cancellationToken);
        }

        var created = await CreateWithUniqueUserNameAsync(identity, cancellationToken);

        var linked = await users.LinkExternalLoginAsync(created.Id, login, avatarUrl: null, cancellationToken);
        jobs.Enqueue<IWelcomeEmailJob>(job => job.ExecuteAsync(created.Id, CancellationToken.None));
        return linked;
    }

    private static string FullNameOf(GoogleIdentity identity)
    {
        var name = identity.Name?.Trim();
        if (name is { Length: >= AuthValidationRules.FullNameMinLength })
        {
            return name.Length > AuthValidationRules.FullNameMaxLength
                ? name[..AuthValidationRules.FullNameMaxLength]
                : name;
        }

        return identity.Email.Split('@')[0].PadRight(AuthValidationRules.FullNameMinLength, '_');
    }

    private async Task<UserAccount> CreateWithUniqueUserNameAsync(GoogleIdentity identity, CancellationToken cancellationToken)
    {
        // S-05: "sinh UserName tự động" cho tài khoản tạo từ Google. Lần đầu dùng phần trước @ của email;
        // nếu đã có người dùng thì thêm hậu tố số ngẫu nhiên và thử lại.
        var baseName = UserNameBaseFrom(identity.Email);
        var userName = baseName;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await users.CreateAsync(
                    new NewUserAccount(identity.Email, FullNameOf(identity), userName, Password: null, identity.PictureUrl),
                    cancellationToken);
            }
            catch (ConflictException exception)
                when (exception.Code == AuthErrorCodes.UserNameExists && attempt < MaxUserNameAttempts)
            {
                userName = $"{baseName}{Random.Shared.Next(1000, 100000)}";
            }
        }
    }

    private static string UserNameBaseFrom(string email)
    {
        var local = new string([.. email.Split('@')[0].Where(char.IsAsciiLetterOrDigit)]);
        if (local.Length > UserNameBaseMaxLength)
        {
            local = local[..UserNameBaseMaxLength];
        }

        return local.Length >= AuthValidationRules.UserNameMinLength
            ? local
            : local.PadRight(AuthValidationRules.UserNameMinLength, '0');
    }
}
