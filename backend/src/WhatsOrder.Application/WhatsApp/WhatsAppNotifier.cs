using WhatsOrder.Application.Realtime;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.WhatsApp;

/// <summary>
/// Optional WhatsApp channel: builds notification texts and enqueues them for
/// asynchronous dispatch. The customer's checkout response never waits on the Meta API.
/// WhatsApp is NOT required for ordering — in-app real-time delivery is the core path;
/// this channel simply no-ops per recipient when a number is missing, and the
/// dispatcher already skips stores whose plan/config excludes WhatsApp.
/// </summary>
public class WhatsAppNotifier(IWhatsAppNotificationQueue queue) : IOrderChannelNotifier
{
    public async Task OrderCreatedAsync(Store store, Order order, CancellationToken ct = default)
    {
        var lang = store.Settings.DefaultLanguage;

        if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
            await queue.EnqueueAsync(new OutboundWhatsAppNotification(
                store.Id, order.Id, WhatsAppMessageType.OrderConfirmation,
                order.CustomerPhone,
                WhatsAppMessageBuilder.BuildOrderConfirmation(store, order, lang)), ct);

        if (!string.IsNullOrWhiteSpace(store.WhatsAppNumber))
            await queue.EnqueueAsync(new OutboundWhatsAppNotification(
                store.Id, order.Id, WhatsAppMessageType.OwnerNewOrderAlert,
                store.WhatsAppNumber,
                WhatsAppMessageBuilder.BuildOwnerNewOrderAlert(store, order, lang)), ct);
    }

    public async Task OrderStatusChangedAsync(Store store, Order order, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerPhone))
            return;

        await queue.EnqueueAsync(new OutboundWhatsAppNotification(
            store.Id, order.Id, WhatsAppMessageType.OrderStatusUpdate,
            order.CustomerPhone,
            WhatsAppMessageBuilder.BuildStatusUpdate(store, order, store.Settings.DefaultLanguage)), ct);
    }
}
