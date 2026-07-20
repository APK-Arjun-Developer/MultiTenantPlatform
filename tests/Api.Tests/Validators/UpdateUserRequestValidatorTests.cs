using Application.DTOs.Common;
using Application.DTOs.Users;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class UpdateUserRequestValidatorTests
{
    private readonly UpdateUserRequestValidator _validator = new();

    private static UpdateUserRequest ValidRequest() => new()
    {
        FullName = "Bob Smith",
        Email = "bob.smith@example.com",
    };

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequestWithAllOptionalFields_PassesValidation()
    {
        var request = new UpdateUserRequest
        {
            FullName = "Bob Smith",
            Email = "bob@example.com",
            RoleId = Guid.NewGuid(),
            Password = "StrongP@ss1!",
            ProfileFileId = Guid.NewGuid(),
            Address = new AddressRequest { Line1 = "1 St", City = "NY", PostalCode = "10001", Country = "US" },
        };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Email ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyEmail_FailsValidation()
    {
        var request = ValidRequest();
        request.Email = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("not-email")]
    [InlineData("missing@")]
    [InlineData("@nowhere")]
    public void InvalidEmailFormat_FailsValidation(string email)
    {
        var request = ValidRequest();
        request.Email = email;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
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
        request.FullName = new string('B', 201);
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
    public void WeakPassword_FailsValidation()
    {
        var request = ValidRequest();
        request.Password = "weakpass";
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
    public void PasswordMissingDigit_FailsValidation()
    {
        var request = ValidRequest();
        request.Password = "Password@!!!";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    // ── RoleId (optional) ─────────────────────────────────────────────────────

    [Fact]
    public void NullRoleId_PassesValidation()
    {
        var request = ValidRequest();
        request.RoleId = null;
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.RoleId);
    }

    [Fact]
    public void EmptyGuidRoleId_FailsValidation()
    {
        var request = ValidRequest();
        request.RoleId = Guid.Empty;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.RoleId);
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
        request.Address = new AddressRequest { Line1 = "", City = "Boston", PostalCode = "02101", Country = "US" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Address.Line1");
    }
}
