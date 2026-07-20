using Api.Contracts;
using Api.Controllers;
using Api.Tests.Helpers;
using Application.Common;
using Application.DTOs.Common;
using Application.DTOs.Roles;
using Application.Exceptions;
using Application.Interfaces.Roles;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Api.Tests.Controllers;

public class RolesControllerTests : ControllerTestBase
{
    private readonly Mock<IRoleService> _roleService = new();

    private RolesController Build(ControllerContext? ctx = null)
    {
        var controller = new RolesController(_roleService.Object);
        controller.ControllerContext = ctx ?? TenantAdminContext();
        return controller;
    }

    // ── Authorization attribute assertions ────────────────────────────────────

    [Theory]
    [InlineData(nameof(RolesController.GetAll), PermissionNames.RolesList)]
    [InlineData(nameof(RolesController.GetByName), PermissionNames.RolesView)]
    [InlineData(nameof(RolesController.GetCurrent), PermissionNames.RolesView)]
    [InlineData(nameof(RolesController.Create), PermissionNames.RolesCreate)]
    [InlineData(nameof(RolesController.Update), PermissionNames.RolesEdit)]
    [InlineData(nameof(RolesController.Delete), PermissionNames.RolesDelete)]
    public void ActionMethod_HasExpectedPermissionAttribute(string methodName, string permission)
    {
        var method = typeof(RolesController).GetMethod(methodName)!;
        var attrs = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .ToList();
        var expectedPolicy = $"{PermissionPolicyProvider.PolicyPrefix}{permission}";
        Assert.Contains(attrs, a => a.Policy == expectedPolicy);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var attr = typeof(RolesController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        Assert.NotEmpty(attr);
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_DefaultParams_ReturnsPagedRoles()
    {
        var paged = MakePagedResponse(MakeRole("Admin"), MakeRole("User"));
        _roleService.Setup(s => s.GetRolesAsync(1, 20, null, null, null, null)).ReturnsAsync(paged);

        var result = await Build().GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<PagedResponse<RoleResponse>>>(ok.Value);
        Assert.Equal("Roles retrieved.", envelope.Message);
        Assert.Equal(2, envelope.Data!.TotalCount);
    }

    [Fact]
    public async Task GetAll_WithSearchAndPermissionFilter_PassesParamsToService()
    {
        var permIds = new List<Guid> { Guid.NewGuid() };
        var paged = MakePagedResponse();
        _roleService.Setup(s => s.GetRolesAsync(1, 10, "admin", permIds, "name", "asc"))
            .ReturnsAsync(paged);

        var result = await Build().GetAll(page: 1, pageSize: 10, search: "admin", permissionIds: permIds, sortBy: "name", sortOrder: "asc");

        Assert.IsType<OkObjectResult>(result);
        _roleService.Verify(s => s.GetRolesAsync(1, 10, "admin", permIds, "name", "asc"), Times.Once);
    }

    // ── GetByName ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByName_ExistingRole_ReturnsOk()
    {
        var role = MakeRole("Manager");
        _roleService.Setup(s => s.GetByNameAsync("Manager")).ReturnsAsync(role);

        var result = await Build().GetByName("Manager");

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<RoleResponse>>(ok.Value);
        Assert.Equal("Role retrieved.", envelope.Message);
        Assert.Equal("Manager", envelope.Data!.Name);
    }

    [Fact]
    public async Task GetByName_RoleNotFound_PropagatesNotFoundException()
    {
        _roleService.Setup(s => s.GetByNameAsync("Unknown"))
            .ThrowsAsync(new NotFoundException("Role 'Unknown' was not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => Build().GetByName("Unknown"));
    }

    // ── GetCurrent ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrent_ReturnsCurrentUserRole()
    {
        var role = MakeRole("TenantUser");
        _roleService.Setup(s => s.GetCurrentRoleAsync()).ReturnsAsync(role);

        var result = await Build().GetCurrent();

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<RoleResponse>>(ok.Value);
        Assert.Equal("Current role retrieved.", envelope.Message);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201WithRole()
    {
        var role = MakeRole("NewRole");
        var request = new CreateRoleRequest { Name = "NewRole", Permissions = [Guid.NewGuid()] };
        _roleService.Setup(s => s.CreateRoleAsync(request)).ReturnsAsync(role);

        var result = await Build().Create(request);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, obj.StatusCode);
        var envelope = Assert.IsType<ApiEnvelope<RoleResponse>>(obj.Value);
        Assert.Equal("Role created.", envelope.Message);
        Assert.Equal("NewRole", envelope.Data!.Name);
    }

    [Fact]
    public async Task Create_DuplicateName_PropagatesConflictException()
    {
        _roleService.Setup(s => s.CreateRoleAsync(It.IsAny<CreateRoleRequest>()))
            .ThrowsAsync(new ConflictException("Role 'Admin' already exists."));

        await Assert.ThrowsAsync<ConflictException>(
            () => Build().Create(new CreateRoleRequest { Name = "Admin", Permissions = [Guid.NewGuid()] }));
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_ReturnsOkWithUpdatedRole()
    {
        var updated = MakeRole("UpdatedRole");
        var request = new UpdateRoleRequest { Name = "OldRole", NewName = "UpdatedRole", Permissions = [Guid.NewGuid()] };
        _roleService.Setup(s => s.UpdateRoleAsync(request)).ReturnsAsync(updated);

        var result = await Build().Update(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<RoleResponse>>(ok.Value);
        Assert.Equal("Role updated.", envelope.Message);
    }

    [Fact]
    public async Task Update_RoleNotFound_PropagatesNotFoundException()
    {
        _roleService.Setup(s => s.UpdateRoleAsync(It.IsAny<UpdateRoleRequest>()))
            .ThrowsAsync(new NotFoundException("Role not found."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => Build().Update(new UpdateRoleRequest { Name = "Ghost", Permissions = [Guid.NewGuid()] }));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingRole_ReturnsOk()
    {
        _roleService.Setup(s => s.DeleteRoleAsync(It.IsAny<DeleteRoleRequest>()))
            .Returns(Task.CompletedTask);

        var result = await Build().Delete("Manager");

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiEnvelope<object?>>(ok.Value);
        Assert.Contains("deleted", envelope.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_RoleNotFound_PropagatesNotFoundException()
    {
        _roleService.Setup(s => s.DeleteRoleAsync(It.IsAny<DeleteRoleRequest>()))
            .ThrowsAsync(new NotFoundException("Role 'Ghost' was not found."));

        await Assert.ThrowsAsync<NotFoundException>(() => Build().Delete("Ghost"));
    }

    [Fact]
    public async Task Delete_PassesRoleNameToService()
    {
        _roleService.Setup(s => s.DeleteRoleAsync(It.IsAny<DeleteRoleRequest>()))
            .Returns(Task.CompletedTask);

        await Build().Delete("SpecificRole");

        _roleService.Verify(s => s.DeleteRoleAsync(
            It.Is<DeleteRoleRequest>(r => r.Name == "SpecificRole")), Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RoleResponse MakeRole(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        TenantId = Guid.NewGuid(),
        PermissionIds = [],
        PermissionNames = [],
    };

    private static PagedResponse<RoleResponse> MakePagedResponse(params RoleResponse[] roles) => new()
    {
        Items = roles,
        Page = 1,
        PageSize = 20,
        TotalCount = roles.Length,
    };
}
