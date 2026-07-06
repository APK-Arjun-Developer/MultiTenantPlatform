using System.Text.Json.Serialization;

namespace Application.DTOs.Auth;

public class StartImpersonationResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string SystemRole { get; set; } = default!;
    public IList<string> Roles { get; set; } = [];
    public DateTime ExpiresAt { get; set; }

    [JsonIgnore]
    public string AccessToken { get; set; } = default!;
}

public class StopImpersonationResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string SystemRole { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }

    [JsonIgnore]
    public string AccessToken { get; set; } = default!;

    [JsonIgnore]
    public string RefreshToken { get; set; } = default!;
}
