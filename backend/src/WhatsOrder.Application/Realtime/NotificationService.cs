using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Orders;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Realtime;

/// <summary>
/// Default <see cref="INotificationService"/>: persist first (owner notifications,
/// system chat messages), then broadcast over SignalR, then hand off to optional
/// channel providers. Every side effect is isolated — one failing channel never
/// affects the others, and nothing here can fail the order operation that triggered it.
/// </summary>
public class NotificationService(
    IAppDbContext db,
    IOrderEventBroadcaster broadcaster,
    IEnumerable<IOrderChannelNotifier> channels,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task NotifyNewOrderAsync(Store store, Order order, CancellationToken ct = default)
    {
        var notification = await TryPersistNotificationAsync(store, order, NotificationType.NewOrder,
            $"#{order.OrderNumber}",
            $"{order.CustomerName} · {Money.Format(order.Total, store.Settings.DefaultLanguage)}", ct);

        await TryAsync(() => broadcaster.OrderCreatedAsync(store.Id, order.ToListItemDto(), ct), "broadcast orderCreated");
        if (notification is not null)
            await TryAsync(() => broadcaster.NotificationAsync(store.Id, notification.ToDto(), ct), "broadcast notification");

        foreach (var channel in channels)
            await TryAsync(() => channel.OrderCreatedAsync(store, order, ct), $"channel {channel.GetType().Name}");
    }

    public async Task NotifyOrderStatusChangedAsync(
        Store store, Order order, OrderStatus previousStatus, CancellationToken ct = default)
    {
        // System chat message so the conversation itself tells the story. Stored as a
        // machine token; each side renders it in its own language. Born read.
        var systemMessage = new ChatMessage
        {
            OrderId = order.Id,
            StoreId = store.Id,
            Sender = ChatSender.System,
            Body = $"status:{order.Status}",
            ReadAt = DateTime.UtcNow
        };
        try
        {
            db.ChatMessages.Add(systemMessage);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist system chat message for order {OrderNumber}", order.OrderNumber);
            systemMessage = null;
        }

        var evt = new OrderStatusChangedEvent(
            order.Id, order.OrderNumber, order.Status, order.EstimatedReadyAt,
            new OrderStatusHistoryDto(previousStatus, order.Status, "store", order.UpdatedAt));
        await TryAsync(() => broadcaster.OrderStatusChangedAsync(store.Id, evt, ct), "broadcast orderStatusChanged");

        if (systemMessage is not null)
            await TryAsync(() => broadcaster.MessageReceivedAsync(store.Id, systemMessage.ToDto(), ct), "broadcast system message");

        // Buyer account channel: persisted inbox entry (body is a machine token the
        // client localizes) plus a live push to the buyer's personal group.
        if (order.BuyerUserId is { } buyerId)
        {
            await TryAsync(() => broadcaster.BuyerOrderStatusChangedAsync(buyerId, evt, ct), "broadcast buyer status");
            if (await BuyerWantsAsync(buyerId, p => p.NotifyOrderUpdates, ct))
            {
                var notification = await TryPersistBuyerNotificationAsync(buyerId, store, order,
                    NotificationType.OrderStatusChanged,
                    $"#{order.OrderNumber} · {store.Name}", $"status:{order.Status}", ct);
                if (notification is not null)
                    await TryAsync(() => broadcaster.BuyerNotificationAsync(buyerId, notification.ToDto(), ct), "broadcast buyer notification");
            }
        }

        foreach (var channel in channels)
            await TryAsync(() => channel.OrderStatusChangedAsync(store, order, ct), $"channel {channel.GetType().Name}");
    }

    public async Task NotifyNewMessageAsync(Store store, Order order, ChatMessage message, CancellationToken ct = default)
    {
        await TryAsync(() => broadcaster.MessageReceivedAsync(store.Id, message.ToDto(), ct), "broadcast messageReceived");

        var excerpt = message.Body.Length <= 120 ? message.Body : message.Body[..117] + "…";

        // Customer messages notify the owner's inbox.
        if (message.Sender == ChatSender.Customer)
        {
            var notification = await TryPersistNotificationAsync(store, order, NotificationType.NewMessage,
                $"#{order.OrderNumber} · {order.CustomerName}", excerpt, ct);
            if (notification is not null)
                await TryAsync(() => broadcaster.NotificationAsync(store.Id, notification.ToDto(), ct), "broadcast notification");
            return;
        }

        // Store replies notify the buyer's account inbox when the order is linked to one.
        if (message.Sender == ChatSender.Store && order.BuyerUserId is { } buyerId
            && await BuyerWantsAsync(buyerId, p => p.NotifyMessages, ct))
        {
            var notification = await TryPersistBuyerNotificationAsync(buyerId, store, order,
                NotificationType.NewMessage, $"#{order.OrderNumber} · {store.Name}", excerpt, ct);
            if (notification is not null)
                await TryAsync(() => broadcaster.BuyerNotificationAsync(buyerId, notification.ToDto(), ct), "broadcast buyer notification");
        }
    }

    private async Task<Notification?> TryPersistNotificationAsync(
        Store store, Order order, NotificationType type, string title, string body, CancellationToken ct)
    {
        try
        {
            var notification = new Notification
            {
                UserId = store.OwnerId,
                StoreId = store.Id,
                OrderId = order.Id,
                Type = type,
                Title = title,
                Body = body
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(ct);
            return notification;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist {Type} notification for order {OrderNumber}", type, order.OrderNumber);
            return null;
        }
    }

    private async Task<Notification?> TryPersistBuyerNotificationAsync(
        Guid buyerId, Store store, Order order, NotificationType type,
        string title, string body, CancellationToken ct)
    {
        try
        {
            var notification = new Notification
            {
                UserId = buyerId,
                StoreId = store.Id,
                OrderId = order.Id,
                Type = type,
                Title = title,
                Body = body
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(ct);
            return notification;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist buyer {Type} notification for order {OrderNumber}", type, order.OrderNumber);
            return null;
        }
    }

    /// <summary>Notification preference lookup; defaults to true (no profile row yet).</summary>
    private async Task<bool> BuyerWantsAsync(
        Guid buyerId, Func<BuyerProfile, bool> preference, CancellationToken ct)
    {
        try
        {
            var profile = await db.BuyerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == buyerId, ct);
            return profile is null || preference(profile);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load buyer profile {UserId} for notification prefs", buyerId);
            return true;
        }
    }

    private async Task TryAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notification side effect failed: {What}", what);
        }
    }
}
