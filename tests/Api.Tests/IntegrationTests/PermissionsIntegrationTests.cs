using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class PermissionsIntegrationTests : IntegrationTestBase
{
    public PermissionsIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_AsTenantAdmin_ReturnsSeededPermissions()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        // Response is PermissionsCatalogResponse { Items: [...] }
        var items = data.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/v1/permissions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsTenantUser_Returns403()
    {
        UseTenantUserAuth();
        var response = await Client.GetAsync("/api/v1/permissions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
