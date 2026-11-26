using Application.DTOs.Users;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class DeleteUserRequestValidatorTests
{
    private readonly DeleteUserRequestValidator _validator = new();

    [Fact]
    public void ValidEmail_PassesValidation()
    {
        var result = _validator.TestValidate(new DeleteUserRequest { Email = "user@example.com" });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEmail_FailsValidation()
    {
        var result = _validator.TestValidate(new DeleteUserRequest { Email = "" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void NullEmail_FailsValidation()
    {
        var result = _validator.TestValidate(new DeleteUserRequest { Email = null! });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain")]
    public void InvalidEmailFormat_FailsValidation(string email)
    {
        var result = _validator.TestValidate(new DeleteUserRequest { Email = email });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
