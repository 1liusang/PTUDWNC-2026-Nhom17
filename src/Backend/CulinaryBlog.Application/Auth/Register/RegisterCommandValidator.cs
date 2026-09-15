using FluentValidation;

namespace CulinaryBlog.Application.Auth.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .Length(2, 100);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.UserName)
            .NotEmpty()
            .Matches("^[a-zA-Z0-9_.]+$")
            .WithMessage("UserName chỉ được chứa chữ, số, dấu chấm và gạch dưới.")
            .Length(3, 50);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password phải chứa ít nhất 1 chữ hoa.")
            .Matches("[0-9]").WithMessage("Password phải chứa ít nhất 1 chữ số.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password phải chứa ít nhất 1 ký tự đặc biệt.");
    }
}
