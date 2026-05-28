using Application.DTOs.Roles;
using FluentValidation;

namespace Application.Validators;

public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Permissions)
            .NotEmpty()
            .WithMessage("At least one permission id is required.");
    }
}
