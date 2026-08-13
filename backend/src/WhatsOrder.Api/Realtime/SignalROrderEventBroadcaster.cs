using Microsoft.AspNetCore.SignalR;
using WhatsOrder.Api.Hubs;
using WhatsOrder.Application.Orders;
using WhatsOrder.Application.Realtime;

namespace WhatsOrder.Api.Realtime;

/// <summary>
/// SignalR implementation of the broadcast port. Events go to the store group
/// (dashboard) and, where relevant, the order group (the customer's tracking page).
/// Delivery is best-effort by design — state is already persisted upstream.
/// </summary>
public class SignalROrderEventBroadcaster(IHubContext<OrderHub> hub) : IOrderEventBroadcaster
{
    public Task OrderCreatedAsync(Guid storeId, OrderListItemDto order, CancellationToken ct = default) =>
        hub.Clients.Group(OrderHub.StoreGroup(storeId)).SendAsync("orderCreated", order, ct);

    public async Task OrderStatusChangedAsync(Guid storeId, OrderStatusChangedEvent evt, CancellationToken ct = default)
    {
        await hub.Clients.Group(OrderHub.OrderGroup(evt.OrderId)).SendAsync("orderStatusChanged", evt, ct);
        await hub.Clients.Group(OrderHub.StoreGroup(storeId)).SendAsync("orderStatusChanged", evt, ct);
    }

    public async Task MessageReceivedAsync(Guid storeId, ChatMessageDto message, CancellationToken ct = default)
    {
        await hub.Clients.Group(OrderHub.OrderGroup(message.OrderId)).SendAsync("messageReceived", message, ct);
        await hub.Clients.Group(OrderHub.StoreGroup(storeId)).SendAsync("messageReceived", message, ct);
    }

    public async Task MessagesReadAsync(Guid storeId, MessagesReadEvent evt, CancellationToken ct = default)
    {
        await hub.Clients.Group(OrderHub.OrderGroup(evt.OrderId)).SendAsync("messagesRead", evt, ct);
        await hub.Clients.Group(OrderHub.StoreGroup(storeId)).SendAsync("messagesRead", evt, ct);
    }

    public Task NotificationAsync(Guid storeId, NotificationDto notification, CancellationToken ct = default) =>
        hub.Clients.Group(OrderHub.StoreGroup(storeId)).SendAsync("notification", notification, ct);

    public Task BuyerNotificationAsync(Guid userId, NotificationDto notification, CancellationToken ct = default) =>
        hub.Clients.Group(OrderHub.BuyerGroup(userId)).SendAsync("notification", notification, ct);

    public Task BuyerOrderStatusChangedAsync(Guid userId, OrderStatusChangedEvent evt, CancellationToken ct = default) =>
        hub.Clients.Group(OrderHub.BuyerGroup(userId)).SendAsync("orderStatusChanged", evt, ct);
}
