using Application.DTOs.Common;
using Application.DTOs.Users;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class UpdateCurrentUserRequestValidatorTests
{
    private readonly UpdateCurrentUserRequestValidator _validator = new();

    private static UpdateCurrentUserRequest ValidRequest() => new()
    {
        FullName = "Jane Doe",
    };

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequestWithOptionalFields_PassesValidation()
    {
        var request = new UpdateCurrentUserRequest
        {
            FullName = "Jane Doe",
            Password = "StrongP@ss1!",
            ProfileFileId = Guid.NewGuid(),
            Address = new AddressRequest { Line1 = "1 Main St", City = "Chicago", PostalCode = "60601", Country = "US" },
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── FullName ──────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyFullName_FailsValidation()
    {
        var request = ValidRequest();
        request.FullName = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void FullNameTooLong_FailsValidation()
    {
        var request = ValidRequest();
        request.FullName = new string('J', 201);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    // ── Password (optional) ───────────────────────────────────────────────────

    [Fact]
    public void NullPassword_PassesValidation()
    {
        var request = ValidRequest();
        request.Password = null;
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void EmptyPassword_PassesValidation()
    {
        // Empty string is treated as not provided (whitespace check)
        var request = ValidRequest();
        request.Password = "";
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void PasswordTooShort_FailsValidation()
    {
        var request = ValidRequest();
        request.Password = "Ab1!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void PasswordMissingUppercase_FailsValidation()
    {
        var request = ValidRequest();
        request.Password = "password@1!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void PasswordMissingLowercase_FailsValidation()
    {
        var request = ValidRequest();
        request.Password = "PASSWORD@1!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void PasswordMissingDigit_FailsValidation()
    {
        var request = ValidRequest();
        request.Password = "Password@!!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    // ── ProfileFileId (optional) ──────────────────────────────────────────────

    [Fact]
    public void NullProfileFileId_PassesValidation()
    {
        var request = ValidRequest();
        request.ProfileFileId = null;
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.ProfileFileId);
    }

    [Fact]
    public void EmptyGuidProfileFileId_FailsValidation()
    {
        var request = ValidRequest();
        request.ProfileFileId = Guid.Empty;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ProfileFileId);
    }

    // ── Address (optional) ────────────────────────────────────────────────────

    [Fact]
    public void NullAddress_PassesValidation()
    {
        var request = ValidRequest();
        request.Address = null;
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Address);
    }

    [Fact]
    public void InvalidAddress_FailsOnAddressField()
    {
        var request = ValidRequest();
        request.Address = new AddressRequest { Line1 = "", City = "Chicago", PostalCode = "60601", Country = "US" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Address.Line1");
    }
}
