using Application.DTOs.Tenant;
using FluentValidation;

namespace Application.Validators;

public class DeleteTenantRequestValidator : AbstractValidator<DeleteTenantRequest>
{
    public DeleteTenantRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty);
    }
}
