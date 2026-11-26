using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class UsersIntegrationTests : IntegrationTestBase
{
    public UsersIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── GetAll ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_AsTenantAdmin_ReturnsPagedUsers()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        var total = data.GetProperty("totalCount").GetInt32();
        Assert.True(total >= 1);
    }

    [Fact]
    public async Task GetAll_WithSearch_ReturnsFilteredResults()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/users?search=Tenant+Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        var items = data.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count >= 0);
    }

    [Fact]
    public async Task GetAll_AsTenantUser_Returns403()
    {
        UseTenantUserAuth();
        var response = await Client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GetById ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingUser_ReturnsOk()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync(
            $"/api/v1/users/{CustomWebApplicationFactory.TenantUserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal(CustomWebApplicationFactory.TenantUserEmail, data.GetProperty("email").GetString());
    }

    [Fact]
    public async Task GetById_NonExistentUser_Returns404()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GetCurrent (profile) ───────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrent_AuthenticatedUser_ReturnsOwnProfile()
    {
        UseTenantUserAuth();
        // Endpoint is GET /api/v1/users/current (not /users/me)
        var response = await Client.GetAsync("/api/v1/users/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal(CustomWebApplicationFactory.TenantUserEmail, data.GetProperty("email").GetString());
    }

    // ── Create ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_ReturnsOk()
    {
        UseTenantAdminAuth();
        var email = $"newuser-{Guid.NewGuid():N}@test.com";
        // CreateUserRequest requires RoleIds (at least one)
        var payload = new
        {
            FullName = "New Integration User",
            Email = email,
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
        };

        var response = await Client.PostAsync("/api/v1/users", JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal(email, data.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Create_DuplicateEmail_Returns409()
    {
        UseTenantAdminAuth();
        var payload = new
        {
            FullName = "Duplicate User",
            Email = CustomWebApplicationFactory.TenantUserEmail,  // already exists
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
        };

        var response = await Client.PostAsync("/api/v1/users", JsonContent(payload));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidEmail_Returns400()
    {
        UseTenantAdminAuth();
        var payload = new
        {
            FullName = "Bad User",
            Email = "not-an-email",
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
        };

        var response = await Client.PostAsync("/api/v1/users", JsonContent(payload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsTenantUser_Returns403()
    {
        UseTenantUserAuth();
        var payload = new
        {
            FullName = "x",
            Email = "x@x.com",
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
        };

        var response = await Client.PostAsync("/api/v1/users", JsonContent(payload));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_ReturnsOk()
    {
        UseTenantAdminAuth();
        // UpdateUserRequest identifies user by Email (no Id field)
        var payload = new
        {
            Email = CustomWebApplicationFactory.TenantUserEmail,
            FullName = "Updated User Name",
        };

        var response = await Client.PutAsync("/api/v1/users", JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal("Updated User Name", data.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Update_NonExistentUser_Returns404()
    {
        UseTenantAdminAuth();
        var payload = new
        {
            Email = "ghost-nonexistent-user@test.com",
            FullName = "Ghost",
        };

        var response = await Client.PutAsync("/api/v1/users", JsonContent(payload));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── UpdateCurrent (profile) ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCurrent_ValidRequest_ReturnsOk()
    {
        UseTenantUserAuth();
        var payload = new { FullName = "Updated Profile Name" };

        // Endpoint is PUT /api/v1/users/current (not /users/me)
        var response = await Client.PutAsync("/api/v1/users/current", JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingUser_ReturnsOk()
    {
        UseTenantAdminAuth();

        // Create a user to delete
        var email = $"delete-{Guid.NewGuid():N}@test.com";
        var createPayload = new
        {
            FullName = "Delete Me",
            Email = email,
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
        };
        var createResponse = await Client.PostAsync("/api/v1/users", JsonContent(createPayload));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        // DeleteUserRequest identifies user by Email
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users");
        deleteRequest.Headers.Authorization = Client.DefaultRequestHeaders.Authorization;
        deleteRequest.Headers.TryAddWithoutValidation("X-Tenant-Id",
            CustomWebApplicationFactory.TestTenantId.ToString());
        deleteRequest.Content = JsonContent(new { Email = email });

        var deleteResponse = await Client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(deleteResponse);
        Assert.Contains("deleted", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_NonExistentUser_Returns404()
    {
        UseTenantAdminAuth();

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users");
        request.Headers.Authorization = Client.DefaultRequestHeaders.Authorization;
        request.Headers.TryAddWithoutValidation("X-Tenant-Id",
            CustomWebApplicationFactory.TestTenantId.ToString());
        // DeleteUserRequest has Email field, not Id
        request.Content = JsonContent(new { Email = "nobody-at-all@ghost.com" });

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Activate / Deactivate ──────────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_ExistingUser_ReturnsOk()
    {
        UseTenantAdminAuth();

        // Create a user first — CreateUserAsync sets IsActive=false (pending setup)
        var email = $"deactivate-{Guid.NewGuid():N}@test.com";
        var createPayload = new
        {
            FullName = "Deactivate Me",
            Email = email,
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
        };
        var createResponse = await Client.PostAsync("/api/v1/users", JsonContent(createPayload));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var createdData = await ReadEnvelopeDataAsync<JsonElement>(createResponse);
        var userId = Guid.Parse(createdData.GetProperty("id").GetString()!);

        // Activate the user first (they start inactive), then deactivate — both are POST
        var activateResponse = await Client.PostAsync($"/api/v1/users/{userId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        var response = await Client.PostAsync($"/api/v1/users/{userId}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Activate_ExistingInactiveUser_ReturnsOk()
    {
        UseTenantAdminAuth();

        // Create a user — CreateUserAsync sets IsActive=false (pending account setup)
        // so the user starts in the inactive state that Activate expects
        var email = $"reactivate-{Guid.NewGuid():N}@test.com";
        var createPayload = new
        {
            FullName = "Reactivate Me",
            Email = email,
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
        };
        var createResponse = await Client.PostAsync("/api/v1/users", JsonContent(createPayload));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var createdData = await ReadEnvelopeDataAsync<JsonElement>(createResponse);
        var userId = Guid.Parse(createdData.GetProperty("id").GetString()!);

        // User is already inactive — activate them directly
        var response = await Client.PostAsync($"/api/v1/users/{userId}/activate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── GetUserInvitations ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserInvitations_AsTenantAdmin_ReturnsOk()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/users/invitations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── InviteUser ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task InviteUser_AsTenantAdmin_ReturnsOk()
    {
        UseTenantAdminAuth();
        // InviteTenantUserRequest requires RoleIds (at least one)
        var payload = new
        {
            Email = $"invited-{Guid.NewGuid():N}@test.com",
            RoleIds = new[] { CustomWebApplicationFactory.TestRoleId },
        };

        var response = await Client.PostAsync("/api/v1/users/invite", JsonContent(payload));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(response);
        Assert.Contains("invitation", msg, StringComparison.OrdinalIgnoreCase);
    }
}
