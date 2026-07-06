using Application.DTOs.Subscription;
using Domain.Enums;
using FluentValidation;

namespace Application.Validators;

public class UpdateTenantPlanRequestValidator : AbstractValidator<UpdateTenantPlanRequest>
{
    public UpdateTenantPlanRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty();

        RuleFor(x => x.PlanType)
            .IsInEnum()
            .WithMessage("PlanType must be Free (1) or Pro (2).");
    }
}
