using Application.DTOs.Roles;
using FluentValidation;

namespace Application.Validators;

public class DeleteRoleRequestValidator : AbstractValidator<DeleteRoleRequest>
{
    public DeleteRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
