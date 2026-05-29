using Application.DTOs.Auth;
using FluentValidation;

namespace Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();

        RuleFor(x => x.TenantSlug)
            .MaximumLength(100)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .When(x => !string.IsNullOrWhiteSpace(x.TenantSlug))
            .WithMessage(
                "TenantSlug must be lowercase alphanumeric with optional hyphens. " +
                "Omit TenantSlug for platform SuperAdmin login.");
    }
}
