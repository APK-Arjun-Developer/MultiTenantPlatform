using Api.Contracts;
using Api.Controllers;
using Api.Tests.Helpers;
using Application.Common;
using Application.DTOs.Common;
using Application.DTOs.Invitations;
using Application.DTOs.Onboarding;
using Application.DTOs.Tenant;
using Application.Exceptions;
using Application.Interfaces.Invitations;
using Application.Interfaces.Tenant;
using Domain.Enums;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Api.Tests.Controllers;

public class TenantControllerTests : ControllerTestBase
{
    private readonly Mock<ITenantService> _tenantService = new();
    private readonly Mock<IInvitationService> _invitationService = new();

    private TenantController Build(ControllerContext? ctx = null)
    {
        var controller = new TenantController(_tenantService.Object, _invitationService.Object);
        controller.ControllerContext = ctx ?? SystemAdminContext();
        return controller;
    }

    // ── Authorization attribute assertions ────────────────────────────────────

    [Theory]
    [InlineData(nameof(TenantController.GetAll), PermissionNames.TenantsList)]
    [InlineData(nameof(TenantController.GetById), PermissionNames.TenantsView)]
    [InlineData(nameof(TenantController.GetCurrent), PermissionNames.TenantsView)]
    [InlineData(nameof(TenantController.Onboard), PermissionNames.TenantsCreate)]
    [InlineData(nameof(TenantController.Update), PermissionNames.TenantsEdit)]
    [InlineData(nameof(TenantController.Delete), PermissionNames.TenantsDelete)]
    public void ActionMethod_HasExpectedPermissionAttribute(string methodName, string permission)
    {
        var method = typeof(TenantController).GetMethod(methodName)!;
        var attrs = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .ToList();
        var expectedPolicy = $"{PermissionPolicyProvider.PolicyPrefix}{permission}";
        Assert.Contains(attrs, a => a.Policy == expectedPolicy);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        Assert.NotEmpty(typeof(TenantController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_DefaultParams_ReturnsPagedTenants()
    {
        var paged = MakePagedTenants(MakeTenant("Acme"), MakeTenant("Globex"));
        _tenantService.Setup(s => s.GetTenantsAsync(1, 20, null, null, null, null, null))
            .ReturnsAsync(paged);

        var result = await Build().GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<PagedResponse<TenantResponse>>>(ok.Value);
        Assert.Equal("Tenants retrieved.", envelope.Message);
        Assert.Equal(2, envelope.Data!.TotalCount);
    }

    [Fact]
    public async Task GetAll_WithFilters_PassesParamsToService()
    {
        var paged = MakePagedTenants();
        _tenantService.Setup(s => s.GetTenantsAsync(2, 5, "acme", "name", "asc", true, CreatedVia.Direct))
            .ReturnsAsync(paged);

        await Build().GetAll(page: 2, pageSize: 5, search: "acme", sortBy: "name", sortOrder: "asc", isActive: true, createdVia: CreatedVia.Direct);

        _tenantService.Verify(s => s.GetTenantsAsync(2, 5, "acme", "name", "asc", true, CreatedVia.Direct), Times.Once);
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingTenant_ReturnsOk()
    {
        var tenant = MakeTenant("Acme Corp");
        _tenantService.Setup(s => s.GetByIdAsync(tenant.Id)).ReturnsAsync(tenant);

        var result = await Build().GetById(tenant.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<TenantResponse>>(ok.Value);
        Assert.Equal("Tenant retrieved.", envelope.Message);
        Assert.Equal("Acme Corp", envelope.Data!.Name);
    }

    [Fact]
    public async Task GetById_TenantNotFound_PropagatesNotFoundException()
    {
        var id = Guid.NewGuid();
        _tenantService.Setup(s => s.GetByIdAsync(id))
            .ThrowsAsync(NotFoundException.For("Tenant", id));

        await Assert.ThrowsAsync<NotFoundException>(() => Build().GetById(id));
    }

    // ── GetCurrent ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrent_ReturnsTenantForCurrentUser()
    {
        var tenant = MakeTenant("Current Corp");
        _tenantService.Setup(s => s.GetCurrentAsync()).ReturnsAsync(tenant);

        var result = await Build().GetCurrent();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<TenantResponse>>(ok.Value);
        Assert.Equal("Current tenant retrieved.", envelope.Message);
    }

    // ── Onboard ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Onboard_ValidRequest_ReturnsOkWithOnboardResponse()
    {
        var response = new OnboardTenantResponse
        {
            TenantId = Guid.NewGuid(),
            Name = "New Corp",
            AdminUserId = Guid.NewGuid(),
            AdminEmail = "admin@newcorp.com",
        };
        _tenantService.Setup(s => s.OnboardTenantAsync(It.IsAny<OnboardTenantRequest>()))
            .ReturnsAsync(response);

        var result = await Build().Onboard(MakeOnboardRequest());

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<OnboardTenantResponse>>(ok.Value);
        Assert.Equal("Tenant onboarded.", envelope.Message);
        Assert.Equal("New Corp", envelope.Data!.Name);
    }

    [Fact]
    public async Task Onboard_DuplicateTenantName_PropagatesConflictException()
    {
        _tenantService.Setup(s => s.OnboardTenantAsync(It.IsAny<OnboardTenantRequest>()))
            .ThrowsAsync(new ConflictException("Tenant with that name already exists."));

        await Assert.ThrowsAsync<ConflictException>(() => Build().Onboard(MakeOnboardRequest()));
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_ReturnsOkWithUpdatedTenant()
    {
        var tenantId = Guid.NewGuid();
        var updated = MakeTenant("Updated Corp", tenantId);
        _tenantService.Setup(s => s.UpdateAsync(It.IsAny<UpdateTenantRequest>())).ReturnsAsync(updated);

        var result = await Build().Update(new UpdateTenantRequest { Id = tenantId, Name = "Updated Corp" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<TenantResponse>>(ok.Value);
        Assert.Equal("Tenant updated.", envelope.Message);
        Assert.Equal("Updated Corp", envelope.Data!.Name);
    }

    [Fact]
    public async Task Update_TenantNotFound_PropagatesNotFoundException()
    {
        _tenantService.Setup(s => s.UpdateAsync(It.IsAny<UpdateTenantRequest>()))
            .ThrowsAsync(new NotFoundException("Tenant not found."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => Build().Update(new UpdateTenantRequest { Id = Guid.NewGuid(), Name = "Ghost Corp" }));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ValidRequest_ReturnsOk()
    {
        _tenantService.Setup(s => s.DeleteAsync(It.IsAny<DeleteTenantRequest>()))
            .Returns(Task.CompletedTask);

        var result = await Build().Delete(new DeleteTenantRequest { Id = Guid.NewGuid() });

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("deleted", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_TenantNotFound_PropagatesNotFoundException()
    {
        _tenantService.Setup(s => s.DeleteAsync(It.IsAny<DeleteTenantRequest>()))
            .ThrowsAsync(new NotFoundException("Tenant not found."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => Build().Delete(new DeleteTenantRequest { Id = Guid.NewGuid() }));
    }

    // ── UpdateCurrentAddress (inline authorization check) ─────────────────────

    [Fact]
    public async Task UpdateCurrentAddress_AsTenantAdmin_ReturnsOk()
    {
        var tenant = MakeTenant("Corp");
        _tenantService.Setup(s => s.UpdateCurrentTenantAddressAsync(It.IsAny<UpdateCurrentTenantAddressRequest>()))
            .ReturnsAsync(tenant);

        var result = await Build(TenantAdminContext()).UpdateCurrentAddress(MakeAddressRequest());

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<TenantResponse>>(ok.Value);
        Assert.Equal("Company address updated.", envelope.Message);
    }

    [Fact]
    public async Task UpdateCurrentAddress_AsSystemAdmin_ReturnsForbid()
    {
        var result = await Build(SystemAdminContext()).UpdateCurrentAddress(MakeAddressRequest());

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateCurrentAddress_AsTenantUser_ReturnsForbid()
    {
        var result = await Build(TenantUserContext()).UpdateCurrentAddress(MakeAddressRequest());

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateCurrentAddress_NoSystemRoleClaim_ReturnsForbid()
    {
        var result = await Build(EmptyClaimsContext()).UpdateCurrentAddress(MakeAddressRequest());

        Assert.IsType<ForbidResult>(result);
    }

    // ── GetInvitations ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInvitations_ReturnsPagedInvitations()
    {
        var paged = new PagedResponse<InvitationListItemResponse>
        {
            Items = [new InvitationListItemResponse { Id = Guid.NewGuid(), Email = "inv@example.com", Status = "Pending" }],
            Page = 1, PageSize = 20, TotalCount = 1,
        };
        _invitationService.Setup(s => s.GetTenantCreationInvitationsAsync(1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await Build().GetInvitations();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<PagedResponse<InvitationListItemResponse>>>(ok.Value);
        Assert.Equal(1, envelope.Data!.TotalCount);
    }

    // ── InviteTenant ──────────────────────────────────────────────────────────

    [Fact]
    public async Task InviteTenant_ValidRequest_ReturnsOkWithInviteResponse()
    {
        var inviteResponse = new InviteResponse
        {
            InvitationId = Guid.NewGuid(),
            InvitationType = InvitationType.NewTenant,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        _invitationService.Setup(s => s.InviteTenantAsync(It.IsAny<InviteTenantRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inviteResponse);

        var result = await Build().InviteTenant(
            new InviteTenantRequest { Email = "new-tenant@example.com" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<InviteResponse>>(ok.Value);
        Assert.Contains("invitation sent", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── RevokeInvitation ──────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeInvitation_ValidId_ReturnsOk()
    {
        var invId = Guid.NewGuid();
        _invitationService.Setup(s => s.RevokeInvitationAsync(invId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Build().RevokeInvitation(invId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("revoked", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── ResendInvitation ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResendInvitation_ValidId_ReturnsOk()
    {
        var invId = Guid.NewGuid();
        _invitationService.Setup(s => s.ResendInvitationAsync(invId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await Build().ResendInvitation(invId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("resent", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TenantResponse MakeTenant(string name, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        IsActive = true,
        PlanType = "Free",
        PlanName = "Free",
        PlanFeatures = new(),
    };

    private static PagedResponse<TenantResponse> MakePagedTenants(params TenantResponse[] tenants) => new()
    {
        Items = tenants, Page = 1, PageSize = 20, TotalCount = tenants.Length,
    };

    private static OnboardTenantRequest MakeOnboardRequest() => new()
    {
        Tenant = new OnboardTenantDetails
        {
            Name = "New Corp",
            Address = new AddressRequest { Line1 = "1 Main St", City = "Springfield", PostalCode = "12345", Country = "US" },
        },
        User = new OnboardUserDetails
        {
            FullName = "Admin User",
            Email = "admin@newcorp.com",
            Address = new AddressRequest { Line1 = "1 Admin Rd", City = "Springfield", PostalCode = "12345", Country = "US" },
        },
    };

    private static UpdateCurrentTenantAddressRequest MakeAddressRequest() => new()
    {
        Address = new AddressRequest
        {
            Line1 = "42 Business Ave",
            City = "Metropolis",
            PostalCode = "10001",
            Country = "US",
        },
    };
}
