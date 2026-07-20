using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class RolesIntegrationTests : IntegrationTestBase
{
    public RolesIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── GetAll ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_AsTenantAdmin_ReturnsRoles()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.True(data.GetProperty("totalCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/v1/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsTenantUser_Returns403()
    {
        UseTenantUserAuth();
        var response = await Client.GetAsync("/api/v1/roles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GetByName ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByName_NonExistentRole_Returns404()
    {
        UseTenantAdminAuth();
        // Endpoint is GET /api/v1/roles/{name} (name string, not Guid)
        var response = await Client.GetAsync("/api/v1/roles/NonExistentRole_XYZ_12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Create ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201WithRole()
    {
        UseTenantAdminAuth();
        // Permissions field is List<Guid> (permission IDs, not names)
        var payload = new
        {
            Name = $"TestRole_{Guid.NewGuid():N}",
            Description = "A test role for integration tests",
            Permissions = new[] { CustomWebApplicationFactory.ProfileViewPermId, CustomWebApplicationFactory.FilesViewPermId },
        };

        var response = await Client.PostAsync("/api/v1/roles", JsonContent(payload));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal(payload.Name, data.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        UseTenantAdminAuth();
        var payload = new { Name = "", Description = "desc", Permissions = new[] { CustomWebApplicationFactory.ProfileViewPermId } };

        var response = await Client.PostAsync("/api/v1/roles", JsonContent(payload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsTenantUser_Returns403()
    {
        UseTenantUserAuth();
        var payload = new { Name = "SomeRole", Permissions = new[] { CustomWebApplicationFactory.ProfileViewPermId } };

        var response = await Client.PostAsync("/api/v1/roles", JsonContent(payload));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Create then GetByName ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateThenGetByName_ReturnsCreatedRole()
    {
        UseTenantAdminAuth();
        var roleName = $"GetByName_{Guid.NewGuid():N}";
        var createPayload = new
        {
            Name = roleName,
            Description = "Test role",
            Permissions = new[] { CustomWebApplicationFactory.ProfileViewPermId },
        };

        var createResponse = await Client.PostAsync("/api/v1/roles", JsonContent(createPayload));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Endpoint is GET /api/v1/roles/{name} — use the role name, not an ID
        var getResponse = await Client.GetAsync($"/api/v1/roles/{Uri.EscapeDataString(roleName)}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var data = await ReadEnvelopeDataAsync<JsonElement>(getResponse);
        Assert.Equal(roleName, data.GetProperty("name").GetString());
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingRole_ReturnsOk()
    {
        UseTenantAdminAuth();

        // Create a role first
        var roleName = $"UpdateRole_{Guid.NewGuid():N}";
        var createPayload = new
        {
            Name = roleName,
            Permissions = new[] { CustomWebApplicationFactory.ProfileViewPermId },
        };
        var createResponse = await Client.PostAsync("/api/v1/roles", JsonContent(createPayload));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await ReadEnvelopeDataAsync<JsonElement>(createResponse);
        var roleId = Guid.Parse(created.GetProperty("id").GetString()!);

        // Update it — UpdateRoleRequest identifies role by Name (not Id)
        var updatePayload = new
        {
            Name = roleName,
            NewName = $"{roleName}_Updated",
            Permissions = new[] { CustomWebApplicationFactory.ProfileViewPermId },
        };

        var response = await Client.PutAsync("/api/v1/roles", JsonContent(updatePayload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingRole_ReturnsOk()
    {
        UseTenantAdminAuth();

        // Create a role to delete
        var roleName = $"DeleteRole_{Guid.NewGuid():N}";
        var createPayload = new
        {
            Name = roleName,
            Permissions = new[] { CustomWebApplicationFactory.ProfileViewPermId },
        };
        var createResponse = await Client.PostAsync("/api/v1/roles", JsonContent(createPayload));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Endpoint is DELETE /api/v1/roles/{name} (route param, not body)
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/v1/roles/{Uri.EscapeDataString(roleName)}");
        deleteRequest.Headers.Authorization = Client.DefaultRequestHeaders.Authorization;
        deleteRequest.Headers.TryAddWithoutValidation("X-Tenant-Id",
            CustomWebApplicationFactory.TestTenantId.ToString());

        var response = await Client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(response);
        Assert.Contains("deleted", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_NonExistentRole_Returns404()
    {
        UseTenantAdminAuth();

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete,
            "/api/v1/roles/NonExistentRoleName_XYZ_99999");
        deleteRequest.Headers.Authorization = Client.DefaultRequestHeaders.Authorization;
        deleteRequest.Headers.TryAddWithoutValidation("X-Tenant-Id",
            CustomWebApplicationFactory.TestTenantId.ToString());

        var response = await Client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
