using Application.DTOs.Onboarding;
using FluentValidation;

namespace Application.Validators.Onboarding;

public class InviteTenantUserRequestValidator : AbstractValidator<InviteTenantUserRequest>
{
    public InviteTenantUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.RoleIds)
            .NotEmpty()
            .WithMessage("At least one role is required.");
    }
}
