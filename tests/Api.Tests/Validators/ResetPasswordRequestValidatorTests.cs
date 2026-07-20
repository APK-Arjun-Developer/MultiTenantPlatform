using Application.DTOs.Auth;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class ResetPasswordRequestValidatorTests
{
    private readonly ResetPasswordRequestValidator _validator = new();

    private static ResetPasswordRequest ValidRequest() => new()
    {
        Token = "valid-reset-token",
        NewPassword = "NewP@ssw0rd!",
        ConfirmPassword = "NewP@ssw0rd!",
    };

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Token ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyToken_FailsValidation()
    {
        var request = ValidRequest();
        request.Token = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Token);
    }

    // ── NewPassword ───────────────────────────────────────────────────────────

    [Fact]
    public void EmptyNewPassword_FailsValidation()
    {
        var request = ValidRequest();
        request.NewPassword = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void PasswordTooShort_FailsValidation()
    {
        var request = ValidRequest();
        request.NewPassword = "Ab1!";
        request.ConfirmPassword = "Ab1!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void PasswordMissingUppercase_FailsValidation()
    {
        var request = ValidRequest();
        request.NewPassword = "newp@ssw0rd!";
        request.ConfirmPassword = "newp@ssw0rd!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void PasswordMissingLowercase_FailsValidation()
    {
        var request = ValidRequest();
        request.NewPassword = "NEWP@SSW0RD!";
        request.ConfirmPassword = "NEWP@SSW0RD!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void PasswordMissingDigit_FailsValidation()
    {
        var request = ValidRequest();
        request.NewPassword = "NewP@ssword!";
        request.ConfirmPassword = "NewP@ssword!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public void PasswordMissingSpecialChar_FailsValidation()
    {
        var request = ValidRequest();
        request.NewPassword = "NewPassw0rd";
        request.ConfirmPassword = "NewPassw0rd";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Password must contain at least one special character.");
    }

    // ── ConfirmPassword ───────────────────────────────────────────────────────

    [Fact]
    public void ConfirmPasswordMismatch_FailsValidation()
    {
        var request = ValidRequest();
        request.ConfirmPassword = "DifferentP@ss1!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage("Passwords do not match.");
    }

    [Fact]
    public void EmptyConfirmPassword_FailsValidation()
    {
        var request = ValidRequest();
        request.ConfirmPassword = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    [Fact]
    public void ExactlyEightCharStrongPassword_PassesValidation()
    {
        var password = "Aa1!bcde";
        var request = new ResetPasswordRequest { Token = "tok", NewPassword = password, ConfirmPassword = password };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
