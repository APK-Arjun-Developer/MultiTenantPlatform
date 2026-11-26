using Application.DTOs.Common;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class AddressRequestValidatorTests
{
    private readonly AddressRequestValidator _validator = new();

    private static AddressRequest ValidAddress() => new()
    {
        Line1 = "123 Main Street",
        City = "Springfield",
        PostalCode = "12345",
        Country = "United States",
    };

    [Fact]
    public void ValidAddress_PassesValidation()
    {
        var result = _validator.TestValidate(ValidAddress());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidAddress_WithOptionalFields_PassesValidation()
    {
        var address = ValidAddress();
        address.Line2 = "Suite 100";
        address.State = "IL";

        var result = _validator.TestValidate(address);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Line1 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyLine1_FailsValidation()
    {
        var address = ValidAddress();
        address.Line1 = "";
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.Line1);
    }

    [Fact]
    public void Line1TooLong_FailsValidation()
    {
        var address = ValidAddress();
        address.Line1 = new string('a', 201);
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.Line1);
    }

    [Fact]
    public void Line1MaxLength_PassesValidation()
    {
        var address = ValidAddress();
        address.Line1 = new string('a', 200);
        var result = _validator.TestValidate(address);
        result.ShouldNotHaveValidationErrorFor(x => x.Line1);
    }

    // ── Line2 (optional) ──────────────────────────────────────────────────────

    [Fact]
    public void NullLine2_PassesValidation()
    {
        var address = ValidAddress();
        address.Line2 = null;
        var result = _validator.TestValidate(address);
        result.ShouldNotHaveValidationErrorFor(x => x.Line2);
    }

    [Fact]
    public void Line2TooLong_FailsValidation()
    {
        var address = ValidAddress();
        address.Line2 = new string('b', 201);
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.Line2);
    }

    // ── City ──────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyCity_FailsValidation()
    {
        var address = ValidAddress();
        address.City = "";
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    [Fact]
    public void CityTooLong_FailsValidation()
    {
        var address = ValidAddress();
        address.City = new string('c', 101);
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.City);
    }

    // ── State (optional) ──────────────────────────────────────────────────────

    [Fact]
    public void NullState_PassesValidation()
    {
        var address = ValidAddress();
        address.State = null;
        var result = _validator.TestValidate(address);
        result.ShouldNotHaveValidationErrorFor(x => x.State);
    }

    [Fact]
    public void StateTooLong_FailsValidation()
    {
        var address = ValidAddress();
        address.State = new string('s', 101);
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.State);
    }

    // ── PostalCode ────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyPostalCode_FailsValidation()
    {
        var address = ValidAddress();
        address.PostalCode = "";
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.PostalCode);
    }

    [Fact]
    public void PostalCodeTooLong_FailsValidation()
    {
        var address = ValidAddress();
        address.PostalCode = new string('0', 21);
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.PostalCode);
    }

    // ── Country ───────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyCountry_FailsValidation()
    {
        var address = ValidAddress();
        address.Country = "";
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.Country);
    }

    [Fact]
    public void CountryTooLong_FailsValidation()
    {
        var address = ValidAddress();
        address.Country = new string('x', 101);
        var result = _validator.TestValidate(address);
        result.ShouldHaveValidationErrorFor(x => x.Country);
    }
}
