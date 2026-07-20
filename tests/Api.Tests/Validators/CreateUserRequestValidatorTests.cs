using Application.DTOs.Users;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _validator = new();

    private static CreateUserRequest ValidRequest() => new()
    {
        FullName = "Jane Doe",
        Email = "jane.doe@example.com",
        RoleIds = [Guid.NewGuid()],
    };

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequestWithMultipleRoles_PassesValidation()
    {
        var request = ValidRequest();
        request.RoleIds = [Guid.NewGuid(), Guid.NewGuid()];
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
        request.FullName = new string('A', 201);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void FullNameMaxLength_PassesValidation()
    {
        var request = ValidRequest();
        request.FullName = new string('A', 200);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.FullName);
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
    [InlineData("notanemail")]
    [InlineData("missing.domain@")]
    [InlineData("@nodomain.com")]
    public void InvalidEmailFormat_FailsValidation(string email)
    {
        var request = ValidRequest();
        request.Email = email;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // ── RoleIds ───────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyRoleIds_FailsValidation()
    {
        var request = ValidRequest();
        request.RoleIds = [];
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.RoleIds);
    }

    [Fact]
    public void RoleIdsWithEmptyGuid_FailsValidation()
    {
        var request = ValidRequest();
        request.RoleIds = [Guid.Empty];
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.RoleIds);
    }

    [Fact]
    public void RoleIdsWithMixedValidAndEmptyGuid_FailsValidation()
    {
        var request = ValidRequest();
        request.RoleIds = [Guid.NewGuid(), Guid.Empty];
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.RoleIds);
    }
}
