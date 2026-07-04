using Application.DTOs.Onboarding;
using Application.Validators;
using FluentValidation;

namespace Application.Validators.Onboarding;

public class CreateTenantAdminRequestValidator : AbstractValidator<CreateTenantAdminRequest>
{
    public CreateTenantAdminRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Address)
            .NotNull().WithMessage("Address is required.");

        When(x => x.Address != null, () =>
        {
            RuleFor(x => x.Address!)
                .SetValidator(new AddressRequestValidator());
        });
    }
}
