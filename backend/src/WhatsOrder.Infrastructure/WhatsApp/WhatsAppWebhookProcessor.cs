using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;
using WhatsOrder.Infrastructure.Persistence;

namespace WhatsOrder.Infrastructure.WhatsApp;

/// <summary>
/// Handles verified webhook payloads from Meta: message delivery statuses update the
/// corresponding whatsapp_messages rows; inbound customer messages are persisted.
/// </summary>
public class WhatsAppWebhookProcessor(AppDbContext db, ILogger<WhatsAppWebhookProcessor> logger)
{
    public async Task ProcessAsync(JsonDocument payload, CancellationToken ct = default)
    {
        if (!payload.RootElement.TryGetProperty("entry", out var entries))
            return;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes))
                continue;

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value))
                    continue;

                if (value.TryGetProperty("statuses", out var statuses))
                    await ApplyStatusUpdatesAsync(statuses, ct);

                if (value.TryGetProperty("messages", out var messages))
                    await StoreInboundMessagesAsync(messages, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyStatusUpdatesAsync(JsonElement statuses, CancellationToken ct)
    {
        foreach (var status in statuses.EnumerateArray())
        {
            var waMessageId = status.TryGetProperty("id", out var id) ? id.GetString() : null;
            var statusText = status.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (waMessageId is null || statusText is null)
                continue;

            var message = await db.WhatsAppMessages
                .FirstOrDefaultAsync(m => m.WaMessageId == waMessageId, ct);
            if (message is null)
                continue;

            var newStatus = statusText.ToLowerInvariant() switch
            {
                "sent" => WhatsAppMessageStatus.Sent,
                "delivered" => WhatsAppMessageStatus.Delivered,
                "read" => WhatsAppMessageStatus.Read,
                "failed" => WhatsAppMessageStatus.Failed,
                _ => (WhatsAppMessageStatus?)null
            };
            if (newStatus is null)
                continue;

            // Never regress (a late "delivered" must not overwrite "read").
            if (newStatus > message.Status || newStatus == WhatsAppMessageStatus.Failed)
                message.Status = newStatus.Value;

            if (newStatus == WhatsAppMessageStatus.Failed
                && status.TryGetProperty("errors", out var errors)
                && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                message.Error = first.TryGetProperty("title", out var title)
                    ? title.GetString()
                    : "Delivery failed.";
            }
        }
    }

    private Task StoreInboundMessagesAsync(JsonElement messages, CancellationToken ct)
    {
        foreach (var inbound in messages.EnumerateArray())
        {
            var from = inbound.TryGetProperty("from", out var f) ? f.GetString() : null;
            var waMessageId = inbound.TryGetProperty("id", out var id) ? id.GetString() : null;
            if (from is null)
                continue;

            var body = inbound.TryGetProperty("text", out var text)
                       && text.TryGetProperty("body", out var b)
                ? b.GetString() ?? ""
                : $"[{(inbound.TryGetProperty("type", out var t) ? t.GetString() : "unknown")} message]";

            db.WhatsAppMessages.Add(new WhatsAppMessage
            {
                Direction = WhatsAppDirection.Inbound,
                Type = WhatsAppMessageType.Inbound,
                Phone = "+" + from.TrimStart('+'),
                Body = body,
                WaMessageId = waMessageId,
                Status = WhatsAppMessageStatus.Delivered
            });

            logger.LogInformation("Stored inbound WhatsApp message from {From}", from);
        }

        return Task.CompletedTask;
    }
}
