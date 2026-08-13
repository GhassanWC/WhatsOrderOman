using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.WhatsApp;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;
using WhatsOrder.Infrastructure.Persistence;

namespace WhatsOrder.Infrastructure.WhatsApp;

/// <summary>
/// Background worker: drains the notification queue, records every message in the
/// whatsapp_messages table, and calls the Cloud API for stores whose plan includes
/// WhatsApp notifications.
/// </summary>
public class WhatsAppDispatcher(
    WhatsAppNotificationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notification in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await ProcessAsync(scope.ServiceProvider, notification, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process WhatsApp notification for order {OrderId}",
                    notification.OrderId);
            }
        }
    }

    /// <summary>One notification → one persisted WhatsAppMessage row with final status.</summary>
    public static async Task ProcessAsync(
        IServiceProvider services, OutboundWhatsAppNotification notification, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var client = services.GetRequiredService<IWhatsAppApiClient>();
        var options = services.GetRequiredService<IOptions<WhatsAppOptions>>().Value;

        var message = new WhatsAppMessage
        {
            StoreId = notification.StoreId,
            OrderId = notification.OrderId,
            Direction = WhatsAppDirection.Outbound,
            Type = notification.Type,
            Phone = notification.Phone,
            Body = notification.Body,
            Status = WhatsAppMessageStatus.Pending
        };
        db.WhatsAppMessages.Add(message);
        await db.SaveChangesAsync(ct);

        var plan = await db.Subscriptions
            .Where(s => s.StoreId == notification.StoreId)
            .Select(s => s.Plan)
            .FirstOrDefaultAsync(ct);

        if (!PlanCatalog.For(plan).WhatsAppNotifications)
        {
            message.Status = WhatsAppMessageStatus.Skipped;
            message.Error = "The store's plan does not include WhatsApp notifications.";
        }
        else if (!client.IsConfigured)
        {
            message.Status = WhatsAppMessageStatus.Skipped;
            message.Error = "WhatsApp Cloud API is not configured.";
        }
        else
        {
            var result = options.UseTemplates
                ? await SendViaTemplateAsync(client, options, notification, ct)
                : await client.SendTextAsync(notification.Phone, notification.Body, ct);

            if (result.Success)
            {
                message.Status = WhatsAppMessageStatus.Sent;
                message.WaMessageId = result.WaMessageId;
            }
            else
            {
                message.Status = WhatsAppMessageStatus.Failed;
                message.Error = Truncate(result.Error, 500);
            }
        }

        await db.SaveChangesAsync(ct);

        // Logged at Information/Warning so a live run is verifiable from the console
        // without querying whatsapp_messages.
        var log = services.GetRequiredService<ILogger<WhatsAppDispatcher>>();
        if (message.Status == WhatsAppMessageStatus.Sent)
        {
            log.LogInformation("WhatsApp {Type} sent to {Phone} (wamid {WaMessageId})",
                message.Type, message.Phone, message.WaMessageId);
        }
        else
        {
            log.LogWarning("WhatsApp {Type} to {Phone} was {Status}: {Error}",
                message.Type, message.Phone, message.Status, message.Error);
        }
    }

    private static Task<WhatsAppSendResult> SendViaTemplateAsync(
        IWhatsAppApiClient client, WhatsAppOptions options,
        OutboundWhatsAppNotification notification, CancellationToken ct)
    {
        // Templates carry the full text as a single body parameter; approved template
        // bodies should be "{{1}}". Owner alerts stay free-text (the owner has opted in).
        var template = notification.Type == WhatsAppMessageType.OrderStatusUpdate
            ? options.StatusUpdateTemplate
            : options.OrderConfirmationTemplate;

        return notification.Type == WhatsAppMessageType.OwnerNewOrderAlert
            ? client.SendTextAsync(notification.Phone, notification.Body, ct)
            : client.SendTemplateAsync(notification.Phone, template, options.TemplateLanguage,
                [notification.Body], ct);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
