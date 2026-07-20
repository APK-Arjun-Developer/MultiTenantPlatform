using Application.DTOs.Tenant;
using Application.Validators;
using FluentValidation.TestHelper;

namespace Api.Tests.Validators;

public class DeleteTenantRequestValidatorTests
{
    private readonly DeleteTenantRequestValidator _validator = new();

    [Fact]
    public void ValidId_PassesValidation()
    {
        var result = _validator.TestValidate(new DeleteTenantRequest { Id = Guid.NewGuid() });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyGuidId_FailsValidation()
    {
        var result = _validator.TestValidate(new DeleteTenantRequest { Id = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void DefaultId_FailsValidation()
    {
        var result = _validator.TestValidate(new DeleteTenantRequest());
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}
