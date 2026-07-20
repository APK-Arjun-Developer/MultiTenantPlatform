using Application.DTOs.Roles;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class CreateRoleRequestValidatorTests
{
    private readonly CreateRoleRequestValidator _validator = new();

    private static CreateRoleRequest ValidRequest() => new()
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
    public void ValidRequestWithDescription_PassesValidation()
    {
        var request = ValidRequest();
        request.Description = "Manages team members.";
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
    public void NullName_FailsValidation()
    {
        var request = ValidRequest();
        request.Name = null!;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameTooLong_FailsValidation()
    {
        var request = ValidRequest();
        request.Name = new string('R', 101);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameMaxLength_PassesValidation()
    {
        var request = ValidRequest();
        request.Name = new string('R', 100);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
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

    [Fact]
    public void MultiplePermissions_PassesValidation()
    {
        var request = ValidRequest();
        request.Permissions = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
