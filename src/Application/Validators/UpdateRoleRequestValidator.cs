using Application.DTOs.Roles;
using FluentValidation;

namespace Application.Validators;

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        When(x => x.NewName != null, () =>
        {
            RuleFor(x => x.NewName!)
                .NotEmpty()
                .MaximumLength(100);
        });

        RuleFor(x => x.Permissions)
            .NotEmpty()
            .WithMessage("At least one permission id is required.");
    }
}
