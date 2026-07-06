using Application.DTOs.Tenant;
using FluentValidation;

namespace Application.Validators;

public class UpdateTenantSettingsRequestValidator : AbstractValidator<UpdateTenantSettingsRequest>
{
    public UpdateTenantSettingsRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ProfileFileId)
            .NotEqual(Guid.Empty)
            .When(x => x.ProfileFileId.HasValue);

        RuleFor(x => x.Address)
            .SetValidator(new AddressRequestValidator()!)
            .When(x => x.Address != null);
    }
}
