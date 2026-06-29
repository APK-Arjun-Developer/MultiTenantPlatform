using Application.DTOs.Invitations;
using Application.Validators;
using FluentValidation;

namespace Application.Validators.Invitations;

public class AcceptTenantCreationInvitationRequestValidator
    : AbstractValidator<AcceptTenantCreationInvitationRequest>
{
    public AcceptTenantCreationInvitationRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Phone)
            .MaximumLength(30)
            .When(x => x.Phone != null);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^A-Za-z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        RuleFor(x => x.TenantName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.TenantSlug)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with optional hyphens.");

        RuleFor(x => x.TenantAddress)
            .NotNull().WithMessage("Company address is required.");

        When(x => x.TenantAddress != null, () =>
        {
            RuleFor(x => x.TenantAddress!)
                .SetValidator(new AddressRequestValidator());
        });

        RuleFor(x => x.UserAddress)
            .NotNull().WithMessage("Personal address is required.");

        When(x => x.UserAddress != null, () =>
        {
            RuleFor(x => x.UserAddress!)
                .SetValidator(new AddressRequestValidator());
        });
    }
}
