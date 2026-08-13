using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WhatsOrder.Infrastructure.WhatsApp;

namespace WhatsOrder.Api.Controllers;

[ApiController]
[Route("api/webhooks/whatsapp")]
[AllowAnonymous]
public class WhatsAppWebhookController(
    IOptions<WhatsAppOptions> options,
    WhatsAppWebhookProcessor processor,
    ILogger<WhatsAppWebhookController> logger) : ControllerBase
{
    /// <summary>Meta verification handshake: echo hub.challenge when the verify token matches.</summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expected = options.Value.WebhookVerifyToken;
        if (mode == "subscribe"
            && !string.IsNullOrEmpty(expected)
            && verifyToken == expected
            && challenge is not null)
        {
            return Content(challenge, "text/plain");
        }

        logger.LogWarning("WhatsApp webhook verification failed (mode={Mode})", mode);
        return Forbid();
    }

    /// <summary>
    /// Delivery statuses + inbound messages. The signature over the raw body is validated
    /// with the Meta app secret before anything is processed.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var memory = new MemoryStream();
        await Request.Body.CopyToAsync(memory, ct);
        var rawBody = memory.ToArray();

        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (!WebhookSignatureValidator.IsValid(options.Value.AppSecret, rawBody, signature))
        {
            logger.LogWarning("Rejected WhatsApp webhook with invalid signature");
            return Unauthorized();
        }

        try
        {
            using var payload = JsonDocument.Parse(rawBody);
            await processor.ProcessAsync(payload, ct);
        }
        catch (Exception ex)
        {
            // Always acknowledge quickly — Meta retries aggressively on non-2xx.
            logger.LogError(ex, "Failed to process WhatsApp webhook payload");
        }

        return Ok();
    }
}
