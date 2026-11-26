using System.Net;
using System.Text;
using System.Text.Json;

namespace Api.Tests.IntegrationTests;

[Collection("Integration")]
public class FilesIntegrationTests : IntegrationTestBase
{
    public FilesIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── GetAll ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_AsTenantAdmin_ReturnsOk()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync("/api/v1/files");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        // Data is an array (not paged)
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/v1/files");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsTenantUser_Returns403()
    {
        // TenantUser with no FilesView permission
        var token = GenerateToken(
            CustomWebApplicationFactory.TenantUserId,
            CustomWebApplicationFactory.TenantUserEmail,
            "Test Tenant User",
            CustomWebApplicationFactory.TestTenantId,
            Domain.Enums.SystemRole.TenantUser);
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Tenant-Id", CustomWebApplicationFactory.TestTenantId.ToString());

        var response = await Client.GetAsync("/api/v1/files");
        // TenantUser lacks Files.View — 403 expected
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Upload + Metadata + Delete cycle ──────────────────────────────────────

    [Fact]
    public async Task Upload_ValidTextFile_ReturnsOk()
    {
        UseTenantAdminAuth();

        using var content = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes("Hello integration test file content.");
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "test-upload.txt");

        var response = await Client.PostAsync("/api/v1/files", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadEnvelopeDataAsync<JsonElement>(response);
        Assert.Equal("test-upload.txt", data.GetProperty("originalName").GetString());
    }

    [Fact]
    public async Task Upload_ThenGetMetadata_ReturnsOk()
    {
        UseTenantAdminAuth();

        // Upload
        using var content = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes("Metadata test file content.");
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "meta-test.txt");

        var uploadResponse = await Client.PostAsync("/api/v1/files", content);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var uploadData = await ReadEnvelopeDataAsync<JsonElement>(uploadResponse);
        var fileId = Guid.Parse(uploadData.GetProperty("id").GetString()!);

        // Get metadata
        var metaResponse = await Client.GetAsync($"/api/v1/files/{fileId}");
        Assert.Equal(HttpStatusCode.OK, metaResponse.StatusCode);
        var metaData = await ReadEnvelopeDataAsync<JsonElement>(metaResponse);
        Assert.Equal("meta-test.txt", metaData.GetProperty("originalName").GetString());
    }

    [Fact]
    public async Task Upload_ThenDownload_ReturnsFileStream()
    {
        UseTenantAdminAuth();

        // Upload
        var fileText = "Download test file content.";
        using var content = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes(fileText);
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "download-test.txt");

        var uploadResponse = await Client.PostAsync("/api/v1/files", content);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var uploadData = await ReadEnvelopeDataAsync<JsonElement>(uploadResponse);
        var fileId = Guid.Parse(uploadData.GetProperty("id").GetString()!);

        // Download
        var downloadResponse = await Client.GetAsync($"/api/v1/files/{fileId}/download");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        var downloaded = await downloadResponse.Content.ReadAsStringAsync();
        Assert.Equal(fileText, downloaded);
    }

    [Fact]
    public async Task Upload_ThenDelete_ReturnsOk()
    {
        UseTenantAdminAuth();

        // Upload
        using var content = new MultipartFormDataContent();
        var fileBytes = Encoding.UTF8.GetBytes("Delete test file content.");
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "delete-test.txt");

        var uploadResponse = await Client.PostAsync("/api/v1/files", content);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var uploadData = await ReadEnvelopeDataAsync<JsonElement>(uploadResponse);
        var fileId = Guid.Parse(uploadData.GetProperty("id").GetString()!);

        // Delete
        var deleteResponse = await Client.DeleteAsync($"/api/v1/files/{fileId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var msg = await ReadEnvelopeMessageAsync(deleteResponse);
        Assert.Contains("deleted", msg, StringComparison.OrdinalIgnoreCase);

        // Verify it's gone
        var metaResponse = await Client.GetAsync($"/api/v1/files/{fileId}");
        Assert.Equal(HttpStatusCode.NotFound, metaResponse.StatusCode);
    }

    [Fact]
    public async Task GetMetadata_NonExistentFile_Returns404()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync($"/api/v1/files/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_NonExistentFile_Returns404()
    {
        UseTenantAdminAuth();
        var response = await Client.GetAsync($"/api/v1/files/{Guid.NewGuid()}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        UseTenantAdminAuth();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Array.Empty<byte>());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "empty.txt");

        var response = await Client.PostAsync("/api/v1/files", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
