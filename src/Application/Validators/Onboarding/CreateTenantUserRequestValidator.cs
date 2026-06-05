using Application.DTOs.Onboarding;
using FluentValidation;

namespace Application.Validators.Onboarding;

public class CreateTenantUserRequestValidator : AbstractValidator<CreateTenantUserRequest>
{
    public CreateTenantUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.RoleNames)
            .NotEmpty()
            .WithMessage("At least one role name is required.");
    }
}
