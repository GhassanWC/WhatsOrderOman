using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Dashboard;

namespace WhatsOrder.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Owner")]
public class DashboardController(DashboardService dashboardService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(CancellationToken ct) =>
        Ok(await dashboardService.GetSummaryAsync(ct));
}
