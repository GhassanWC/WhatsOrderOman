using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsOrder.Application.Realtime;

namespace WhatsOrder.Api.Controllers;

/// <summary>
/// The caller's persisted notification inbox (missed events survive offline periods).
/// Scoped by the authenticated user id, so it serves both store owners and buyers.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(NotificationInbox inbox) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationsPage>> List([FromQuery] int limit = 30, CancellationToken ct = default) =>
        Ok(await inbox.ListAsync(limit, ct));

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllRead(CancellationToken ct)
    {
        await inbox.MarkAllReadAsync(ct);
        return NoContent();
    }
}
