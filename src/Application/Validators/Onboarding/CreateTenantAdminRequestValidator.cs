using Application.DTOs.Onboarding;
using FluentValidation;

namespace Application.Validators.Onboarding;

public class CreateTenantAdminRequestValidator : AbstractValidator<CreateTenantAdminRequest>
{
    public CreateTenantAdminRequestValidator()
    {
        RuleFor(x => x.TenantSlug)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
