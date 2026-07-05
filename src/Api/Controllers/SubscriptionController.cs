using Api.Attributes;
using Application.Common;
using Application.DTOs.Subscription;
using Application.Interfaces.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/subscriptions")]
[Authorize]
public class SubscriptionController : ApiControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("plans")]
    [HasPermission(PermissionNames.SubscriptionsView)]
    public IActionResult GetPlans()
    {
        var plans = _subscriptionService.GetPlans();
        return OkEnvelope(plans, "Subscription plans retrieved.");
    }

    [HttpPut("tenant-plan")]
    [HasPermission(PermissionNames.SubscriptionsEdit)]
    public async Task<IActionResult> UpdateTenantPlan(
        UpdateTenantPlanRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _subscriptionService.UpdateTenantPlanAsync(request, cancellationToken);
        return OkEnvelope(response, $"Tenant plan updated to {response.PlanName}.");
    }
}
