using Api.Attributes;
using Application.DTOs.Emails;
using Application.Interfaces.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/emails")]
[Authorize]
public class EmailBroadcastController : ApiControllerBase
{
    private readonly IEmailBroadcastService _broadcastService;

    public EmailBroadcastController(IEmailBroadcastService broadcastService)
    {
        _broadcastService = broadcastService;
    }

    [HttpPost("broadcast")]
    [HasPermission("Emails.Send")]
    public async Task<IActionResult> Broadcast(
        SendEmailBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _broadcastService.BroadcastAsync(request, cancellationToken);
        return OkEnvelope(response, response.Message);
    }
}
