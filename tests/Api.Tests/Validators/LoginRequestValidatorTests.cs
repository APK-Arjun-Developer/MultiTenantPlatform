using Application.DTOs.Auth;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new LoginRequest { Email = "user@example.com", Password = "P@ssw0rd!" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyEmail_FailsWithEmailError()
    {
        var result = _validator.TestValidate(new LoginRequest { Email = "", Password = "P@ssw0rd!" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("double@@signs.com")]
    public void InvalidEmailFormat_FailsWithEmailError(string email)
    {
        var result = _validator.TestValidate(new LoginRequest { Email = email, Password = "P@ssw0rd!" });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void EmptyPassword_FailsValidation()
    {
        var result = _validator.TestValidate(new LoginRequest { Email = "user@example.com", Password = "" });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void NullPassword_FailsValidation()
    {
        var result = _validator.TestValidate(new LoginRequest { Email = "user@example.com", Password = null! });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void ValidMinimalCredentials_PassesValidation()
    {
        var result = _validator.TestValidate(new LoginRequest { Email = "a@b.co", Password = "x" });
        result.ShouldNotHaveAnyValidationErrors();
    }
}
