using Application.DTOs.Common;
using FluentValidation;

namespace Application.Validators;

public class AddressRequestValidator : AbstractValidator<AddressRequest>
{
    public AddressRequestValidator()
    {
        RuleFor(x => x.Line1)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Line2)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Line2));

        RuleFor(x => x.City)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.State)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.State));

        RuleFor(x => x.PostalCode)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.Country)
            .NotEmpty()
            .MaximumLength(100);
    }
}
