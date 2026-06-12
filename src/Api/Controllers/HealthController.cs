using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1")]
[AllowAnonymous]
public class HealthController : ApiControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public HealthController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet("health")]
    public IActionResult Get()
    {
        var appVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        return OkEnvelope(
            new
            {
                status = "healthy",
                version = "v1",
                appVersion,
                environment = _environment.EnvironmentName,
                timestampUtc = DateTime.UtcNow,
            },
            "API is running.");
    }
}
