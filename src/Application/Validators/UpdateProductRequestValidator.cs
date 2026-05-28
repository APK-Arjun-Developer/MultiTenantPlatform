using Application.DTOs.Products;
using FluentValidation;

namespace Application.Validators;

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NewName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.NewName));

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0);
    }
}
