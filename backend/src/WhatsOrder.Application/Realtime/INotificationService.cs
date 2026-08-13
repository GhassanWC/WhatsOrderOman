using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Realtime;

/// <summary>
/// Single entry point for "something happened on an order" side effects: persisted
/// owner notifications, real-time broadcast, and optional channel providers
/// (WhatsApp today; SMS later). Order services never talk to a provider directly.
/// Implementations must never throw — an order is already saved when this runs.
/// </summary>
public interface INotificationService
{
    Task NotifyNewOrderAsync(Store store, Order order, CancellationToken ct = default);

    Task NotifyOrderStatusChangedAsync(Store store, Order order, OrderStatus previousStatus, CancellationToken ct = default);

    Task NotifyNewMessageAsync(Store store, Order order, ChatMessage message, CancellationToken ct = default);
}

/// <summary>
/// An optional outbound channel (WhatsApp, SMS…). Implementations decide themselves
/// whether they are configured/applicable and no-op otherwise.
/// </summary>
public interface IOrderChannelNotifier
{
    Task OrderCreatedAsync(Store store, Order order, CancellationToken ct = default);
    Task OrderStatusChangedAsync(Store store, Order order, CancellationToken ct = default);
}
