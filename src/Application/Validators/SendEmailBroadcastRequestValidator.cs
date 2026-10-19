using Application.DTOs.Emails;
using FluentValidation;

namespace Application.Validators;

public class SendEmailBroadcastRequestValidator : AbstractValidator<SendEmailBroadcastRequest>
{
    public SendEmailBroadcastRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(200);

        RuleFor(x => x.HtmlBody)
            .NotEmpty().WithMessage("Email body is required.");

        When(x => x.TargetType == BroadcastTarget.SpecificTenants, () =>
            RuleFor(x => x.TenantIds)
                .NotEmpty().WithMessage("Select at least one tenant."));

        When(x => x.TargetType == BroadcastTarget.SpecificUsers, () =>
            RuleFor(x => x.UserIds)
                .NotEmpty().WithMessage("Select at least one user."));
    }
}
