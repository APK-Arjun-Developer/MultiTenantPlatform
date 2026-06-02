using Application.DTOs.Users;
using FluentValidation;

namespace Application.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Address)
            .SetValidator(new AddressRequestValidator()!)
            .When(x => x.Address != null);
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Password)
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .When(x => !string.IsNullOrWhiteSpace(x.Password));

        RuleFor(x => x.ProfileFileId)
            .NotEqual(Guid.Empty)
            .When(x => x.ProfileFileId.HasValue);
    }
}
