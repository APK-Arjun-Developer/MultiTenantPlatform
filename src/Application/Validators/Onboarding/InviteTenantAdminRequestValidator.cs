using Application.DTOs.Onboarding;
using FluentValidation;

namespace Application.Validators.Onboarding;

public class InviteTenantAdminRequestValidator : AbstractValidator<InviteTenantAdminRequest>
{
    public InviteTenantAdminRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
