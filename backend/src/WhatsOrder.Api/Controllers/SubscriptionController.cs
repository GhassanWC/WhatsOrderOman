using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Subscriptions;

namespace WhatsOrder.Api.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize(Roles = "Owner")]
public class SubscriptionController(SubscriptionService subscriptionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SubscriptionDto>> Get(CancellationToken ct) =>
        Ok(await subscriptionService.GetAsync(ct));

    /// <summary>MVP plan switch (no billing yet — a payment provider slots in here later).</summary>
    [HttpPost("plan")]
    public async Task<ActionResult<SubscriptionDto>> ChangePlan(ChangePlanRequest request, CancellationToken ct) =>
        Ok(await subscriptionService.ChangePlanAsync(request, ct));
}
