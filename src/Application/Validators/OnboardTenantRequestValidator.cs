using Application.DTOs.Tenant;
using FluentValidation;

namespace Application.Validators;

public class OnboardTenantRequestValidator : AbstractValidator<OnboardTenantRequest>
{
    public OnboardTenantRequestValidator()
    {
        RuleFor(x => x.Tenant)
            .NotNull()
            .SetValidator(new OnboardTenantDetailsValidator());

        RuleFor(x => x.User)
            .NotNull()
            .SetValidator(new OnboardUserDetailsValidator());

        // Roles are optional — TenantAdmin and TenantUser are always auto-created.
        RuleForEach(x => x.Roles)
            .SetValidator(new OnboardRoleDetailsValidator());
    }
}

public class OnboardTenantDetailsValidator : AbstractValidator<OnboardTenantDetails>
{
    public OnboardTenantDetailsValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with optional hyphens.");
    }
}

public class OnboardUserDetailsValidator : AbstractValidator<OnboardUserDetails>
{
    public OnboardUserDetailsValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

public class OnboardRoleDetailsValidator : AbstractValidator<OnboardRoleDetails>
{
    public OnboardRoleDetailsValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Permissions)
            .NotEmpty()
            .WithMessage("Each role must have at least one permission id.");
    }
}
