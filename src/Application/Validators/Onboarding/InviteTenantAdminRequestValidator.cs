using Application.DTOs.Onboarding;
using FluentValidation;

namespace Application.Validators.Onboarding;

public class InviteTenantAdminRequestValidator : AbstractValidator<InviteTenantAdminRequest>
{
    public InviteTenantAdminRequestValidator()
    {
        RuleFor(x => x.TenantSlug)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
