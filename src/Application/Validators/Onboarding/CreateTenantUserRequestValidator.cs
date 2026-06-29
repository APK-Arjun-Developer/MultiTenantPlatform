using Application.DTOs.Onboarding;
using Application.Validators;
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

        RuleFor(x => x.RoleIds)
            .NotEmpty().WithMessage("At least one role is required.")
            .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage("Role IDs must be valid GUIDs.");

        RuleFor(x => x.Address)
            .NotNull().WithMessage("Address is required.");

        When(x => x.Address != null, () =>
        {
            RuleFor(x => x.Address!)
                .SetValidator(new AddressRequestValidator());
        });
    }
}
