using Application.DTOs.Users;
using FluentValidation;

namespace Application.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Password)
            .MinimumLength(8)
            .When(x => !string.IsNullOrWhiteSpace(x.Password));
    }
}
