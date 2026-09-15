using FluentValidation;

namespace CulinaryBlog.Application.Auth.UpdateProfile;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.FullName)
            .Length(2, 100)
            .When(x => x.FullName is not null);

        RuleFor(x => x.AvatarUrl)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("AvatarUrl phải là một URL hợp lệ.")
            .When(x => x.AvatarUrl is not null);
    }
}
