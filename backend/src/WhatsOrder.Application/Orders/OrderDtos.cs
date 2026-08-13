using WhatsOrder.Application.Realtime;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Orders;

public sealed record PublicOrderItemRequest(Guid ProductId, int Quantity, List<Guid>? OptionIds);

public sealed record CreatePublicOrderRequest(
    string CustomerName,
    string CustomerPhone,
    FulfillmentMethod FulfillmentMethod,
    string? DeliveryAddress,
    string? GoogleMapsUrl,
    string? PreferredTime,
    string? Notes,
    List<PublicOrderItemRequest> Items);

public sealed record OrderItemDto(
    Guid Id, Guid? ProductId, string ProductName, string? VariantsText,
    decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhone,
    OrderStatus Status,
    FulfillmentMethod FulfillmentMethod,
    string? DeliveryAddress,
    string? GoogleMapsUrl,
    string? PreferredTime,
    string? Notes,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Discount,
    decimal Total,
    List<OrderItemDto> Items,
    DateTime? EstimatedReadyAt,
    List<OrderStatusHistoryDto> StatusHistory,
    int UnreadMessages,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record OrderListItemDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerPhone,
    OrderStatus Status,
    FulfillmentMethod FulfillmentMethod,
    decimal Total,
    int ItemsCount,
    string ItemsSummary,
    int UnreadMessages,
    DateTime CreatedAt);

public sealed record OrdersPage(
    IReadOnlyList<OrderListItemDto> Items, int Total, int Page, int PageSize, int NewCount);

/// <summary>EstimatedMinutes optionally sets/updates the "ready in ~X min" promise.</summary>
public sealed record UpdateOrderStatusRequest(OrderStatus Status, int? EstimatedMinutes = null);

public sealed record PublicOrderCreatedDto(
    string OrderNumber,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Discount,
    decimal Total,
    OrderStatus Status,
    string StoreWhatsAppNumber,
    string? AppliedOffer = null,
    string? AppliedOfferAr = null);

public sealed record PublicOrderStatusDto(
    Guid OrderId,
    string OrderNumber,
    OrderStatus Status,
    FulfillmentMethod FulfillmentMethod,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    List<OrderItemDto> Items,
    DateTime? EstimatedReadyAt,
    List<OrderStatusHistoryDto> StatusHistory,
    DateTime CreatedAt);

public static class OrderMapping
{
    public static OrderItemDto ToDto(this OrderItem item) => new(
        item.Id, item.ProductId, item.ProductName, item.VariantsText,
        item.UnitPrice, item.Quantity, item.LineTotal);

    public static OrderDto ToDto(this Order order, int unreadMessages = 0) => new(
        order.Id,
        order.OrderNumber,
        order.CustomerName,
        order.CustomerPhone,
        order.Status,
        order.FulfillmentMethod,
        order.DeliveryAddress,
        order.GoogleMapsUrl,
        order.PreferredTime,
        order.Notes,
        order.Subtotal,
        order.DeliveryFee,
        order.Discount,
        order.Total,
        order.Items.Select(i => i.ToDto()).ToList(),
        order.EstimatedReadyAt,
        order.StatusHistory.OrderBy(h => h.CreatedAt).Select(h => h.ToDto()).ToList(),
        unreadMessages,
        order.CreatedAt,
        order.UpdatedAt);

    public static OrderListItemDto ToListItemDto(this Order order, int unreadMessages = 0)
    {
        var summary = string.Join(", ", order.Items.Select(i => $"{i.Quantity}× {i.ProductName}"));
        if (summary.Length > 90)
            summary = summary[..87] + "…";

        return new OrderListItemDto(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            order.CustomerPhone,
            order.Status,
            order.FulfillmentMethod,
            order.Total,
            order.Items.Sum(i => i.Quantity),
            summary,
            unreadMessages,
            order.CreatedAt);
    }
}
