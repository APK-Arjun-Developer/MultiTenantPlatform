using Application.DTOs.Common;
using Application.DTOs.Tenant;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class OnboardTenantRequestValidatorTests
{
    private readonly OnboardTenantRequestValidator _validator = new();

    private static AddressRequest ValidAddress() => new()
    {
        Line1 = "1 Business Rd",
        City = "Chicago",
        PostalCode = "60601",
        Country = "United States",
    };

    private static OnboardTenantRequest ValidRequest() => new()
    {
        Tenant = new OnboardTenantDetails
        {
            Name = "Acme Corp",
            Address = ValidAddress(),
        },
        User = new OnboardUserDetails
        {
            FullName = "Alice Admin",
            Email = "alice@acmecorp.com",
            Address = ValidAddress(),
        },
        Roles = [],
    };

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequestWithRoles_PassesValidation()
    {
        var request = ValidRequest();
        request.Roles.Add(new OnboardRoleDetails
        {
            Name = "Viewer",
            Permissions = [Guid.NewGuid()],
        });
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Tenant ────────────────────────────────────────────────────────────────

    [Fact]
    public void NullTenant_FailsValidation()
    {
        var request = ValidRequest();
        request.Tenant = null!;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Tenant);
    }

    [Fact]
    public void EmptyTenantName_FailsValidation()
    {
        var request = ValidRequest();
        request.Tenant.Name = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Tenant.Name");
    }

    [Fact]
    public void TenantNameTooLong_FailsValidation()
    {
        var request = ValidRequest();
        request.Tenant.Name = new string('T', 201);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Tenant.Name");
    }

    [Fact]
    public void NullTenantAddress_FailsValidation()
    {
        var request = ValidRequest();
        request.Tenant.Address = null;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Tenant.Address");
    }

    [Fact]
    public void InvalidTenantAddress_FailsOnAddressField()
    {
        var request = ValidRequest();
        request.Tenant.Address!.Line1 = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Tenant.Address.Line1");
    }

    // ── User ──────────────────────────────────────────────────────────────────

    [Fact]
    public void NullUser_FailsValidation()
    {
        var request = ValidRequest();
        request.User = null!;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.User);
    }

    [Fact]
    public void EmptyUserFullName_FailsValidation()
    {
        var request = ValidRequest();
        request.User.FullName = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("User.FullName");
    }

    [Fact]
    public void EmptyUserEmail_FailsValidation()
    {
        var request = ValidRequest();
        request.User.Email = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("User.Email");
    }

    [Fact]
    public void InvalidUserEmail_FailsValidation()
    {
        var request = ValidRequest();
        request.User.Email = "not-an-email";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("User.Email");
    }

    [Fact]
    public void NullUserAddress_FailsValidation()
    {
        var request = ValidRequest();
        request.User.Address = null;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("User.Address");
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RoleWithEmptyName_FailsValidation()
    {
        var request = ValidRequest();
        request.Roles.Add(new OnboardRoleDetails { Name = "", Permissions = [Guid.NewGuid()] });
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Roles[0].Name");
    }

    [Fact]
    public void RoleWithNoPermissions_FailsValidation()
    {
        var request = ValidRequest();
        request.Roles.Add(new OnboardRoleDetails { Name = "Viewer", Permissions = [] });
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Roles[0].Permissions");
    }

    [Fact]
    public void RoleNameTooLong_FailsValidation()
    {
        var request = ValidRequest();
        request.Roles.Add(new OnboardRoleDetails { Name = new string('R', 101), Permissions = [Guid.NewGuid()] });
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Roles[0].Name");
    }
}
