using Application.DTOs.Tenant;
using FluentValidation;

namespace Application.Validators;

public class UpdateTenantRequestValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NewSlug)
            .MaximumLength(100)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .When(x => !string.IsNullOrWhiteSpace(x.NewSlug))
            .WithMessage("Slug must be lowercase alphanumeric with optional hyphens.");
    }
}
