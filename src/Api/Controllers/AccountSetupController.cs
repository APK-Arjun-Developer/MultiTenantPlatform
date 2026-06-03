using Application.DTOs.AccountSetup;
using Application.Interfaces.AccountSetup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Public endpoints for the account-setup (set-password) flow.
/// No authentication required — access is controlled by the short-lived setup token.
/// </summary>
[ApiController]
[Route("api/v1/account-setup")]
[AllowAnonymous]
public class AccountSetupController : ApiControllerBase
{
    private readonly IAccountSetupService _accountSetupService;

    public AccountSetupController(IAccountSetupService accountSetupService)
    {
        _accountSetupService = accountSetupService;
    }

    /// <summary>
    /// Validate an account-setup token before presenting the set-password form.
    /// Returns the user's email and name so the frontend can pre-fill the form.
    /// </summary>
    [HttpGet("validate")]
    public async Task<IActionResult> Validate(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var response = await _accountSetupService.ValidateTokenAsync(token, cancellationToken);

        return OkEnvelope(response, response.IsValid
            ? "Token is valid."
            : "Token validation failed.");
    }

    /// <summary>
    /// Complete account setup: set the user's password and activate their account.
    /// The token is consumed and becomes single-use after this call.
    /// </summary>
    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword(
        SetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _accountSetupService.SetPasswordAsync(request, cancellationToken);

        return OkEnvelope(response, "Account setup complete. You can now log in.");
    }
}
