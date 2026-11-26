using Application.DTOs.Roles;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class UpdateRoleRequestValidatorTests
{
    private readonly UpdateRoleRequestValidator _validator = new();

    private static UpdateRoleRequest ValidRequest() => new()
    {
        Name = "Manager",
        Permissions = [Guid.NewGuid()],
    };

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequestWithNewName_PassesValidation()
    {
        var request = ValidRequest();
        request.NewName = "Senior Manager";
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequestWithNullNewName_PassesValidation()
    {
        var request = ValidRequest();
        request.NewName = null;
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
        request.Name = new string('N', 101);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    // ── NewName (conditional) ─────────────────────────────────────────────────

    [Fact]
    public void EmptyNewName_WhenProvided_FailsValidation()
    {
        var request = ValidRequest();
        request.NewName = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewName);
    }

    [Fact]
    public void NewNameTooLong_FailsValidation()
    {
        var request = ValidRequest();
        request.NewName = new string('N', 101);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewName);
    }

    // ── Permissions ───────────────────────────────────────────────────────────

    [Fact]
    public void EmptyPermissions_FailsValidation()
    {
        var request = ValidRequest();
        request.Permissions = [];
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Permissions);
    }
}
