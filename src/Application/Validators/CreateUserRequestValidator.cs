using Application.DTOs.Users;
using FluentValidation;

namespace Application.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
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
    }
}
