using Application.DTOs.Invitations;
using FluentValidation;

namespace Application.Validators.Invitations;

public class AcceptTenantAdminInvitationRequestValidator
    : AbstractValidator<AcceptTenantAdminInvitationRequest>
{
    public AcceptTenantAdminInvitationRequestValidator()
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
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
