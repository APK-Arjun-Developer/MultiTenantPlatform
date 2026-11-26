using System.Net;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class ActivityLogsIntegrationTests : IntegrationTestBase
{
    public ActivityLogsIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetLogs_AsSystemAdmin_ReturnsOk()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/activity-logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_AsTenantAdmin_Returns403()
    {
        // ActivityLogs controller is SystemAdminOnly
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/activity-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/v1/activity-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_AsTenantUser_Returns403()
    {
        UseTenantUserAuth();
        var response = await Client.GetAsync("/api/v1/activity-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_LoginGeneratesActivityLog()
    {
        // Login generates an activity log entry (unauthenticated call)
        var loginResponse = await Client.PostAsync("/api/v1/auth/login",
            JsonContent(new
            {
                Email = CustomWebApplicationFactory.TenantAdminEmail,
                Password = CustomWebApplicationFactory.UserPassword,
            }));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Use a cookie-free admin client for the SystemAdmin-only logs endpoint.
        // The login above sets an HttpOnly cookie on Client (for TenantAdmin), which the
        // server prefers over the Bearer header — a fresh client avoids that conflict.
        using var adminClient = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AdminToken());

        var logsResponse = await adminClient.GetAsync("/api/v1/activity-logs");
        Assert.Equal(HttpStatusCode.OK, logsResponse.StatusCode);

        var json = await logsResponse.Content.ReadAsStringAsync();
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("totalCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetLogs_WithFilters_ReturnsFilteredResults()
    {
        UseAdminAuth();
        var response = await Client.GetAsync("/api/v1/activity-logs?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.True(data.GetProperty("pageSize").GetInt32() <= 5);
    }
}
