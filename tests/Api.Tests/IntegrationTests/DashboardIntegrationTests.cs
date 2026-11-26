using System.Net;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class DashboardIntegrationTests : IntegrationTestBase
{
    public DashboardIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetDashboard_AsTenantAdmin_ReturnsOk()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/dashboard/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_AsSystemAdmin_ReturnsOk()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/dashboard/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/v1/dashboard/stats");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
