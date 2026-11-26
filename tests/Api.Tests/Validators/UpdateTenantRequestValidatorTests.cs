using Application.DTOs.Common;
using Application.DTOs.Tenant;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class UpdateTenantRequestValidatorTests
{
    private readonly UpdateTenantRequestValidator _validator = new();

    private static UpdateTenantRequest ValidRequest() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Acme Corp",
    };

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequestWithAddress_PassesValidation()
    {
        var request = ValidRequest();
        request.Address = new AddressRequest { Line1 = "1 Corp Way", City = "Boston", PostalCode = "02101", Country = "US" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequestWithProfileFileId_PassesValidation()
    {
        var request = ValidRequest();
        request.ProfileFileId = Guid.NewGuid();
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Name ──────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyName_FailsValidation()
    {
        var request = ValidRequest();
        request.Name = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameTooLong_FailsValidation()
    {
        var request = ValidRequest();
        request.Name = new string('A', 201);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameMaxLength_PassesValidation()
    {
        var request = ValidRequest();
        request.Name = new string('A', 200);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
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
