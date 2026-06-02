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

        RuleFor(x => x.Roles)
            .NotEmpty()
            .WithMessage("At least one role is required.");

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

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
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
