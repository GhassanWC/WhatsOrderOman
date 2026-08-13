using WhatsOrder.Application.Orders;

namespace WhatsOrder.Application.Realtime;

/// <summary>
/// Real-time fan-out (implemented over SignalR in the Api layer). Purely best-effort
/// delivery: every event's source of truth is already persisted before this is called,
/// so a disconnected party misses nothing — they reload state from the REST API.
/// </summary>
public interface IOrderEventBroadcaster
{
    /// <summary>New order → the store's dashboard group.</summary>
    Task OrderCreatedAsync(Guid storeId, OrderListItemDto order, CancellationToken ct = default);

    /// <summary>Status change → the order's group (customer) and the store group (dashboard).</summary>
    Task OrderStatusChangedAsync(Guid storeId, OrderStatusChangedEvent evt, CancellationToken ct = default);

    /// <summary>Chat message → the order's group and the store group.</summary>
    Task MessageReceivedAsync(Guid storeId, ChatMessageDto message, CancellationToken ct = default);

    /// <summary>Read receipt → the order's group.</summary>
    Task MessagesReadAsync(Guid storeId, MessagesReadEvent evt, CancellationToken ct = default);

    /// <summary>Persisted owner notification → the store group (live bell update).</summary>
    Task NotificationAsync(Guid storeId, NotificationDto notification, CancellationToken ct = default);

    /// <summary>Persisted buyer notification → the buyer's personal group (account badge).</summary>
    Task BuyerNotificationAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);

    /// <summary>Status change → the buyer's personal group (orders list stays live).</summary>
    Task BuyerOrderStatusChangedAsync(Guid userId, OrderStatusChangedEvent evt, CancellationToken ct = default);
}
