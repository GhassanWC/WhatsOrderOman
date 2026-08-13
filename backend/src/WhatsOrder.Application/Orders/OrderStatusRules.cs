using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Orders;

public static class OrderStatusRules
{
    /// <summary>
    /// Completed, Cancelled and Rejected are terminal. Any non-terminal order can be
    /// cancelled; rejecting is only possible while the order is still New (after that,
    /// cancel instead). Otherwise only forward movement is allowed (skipping steps is
    /// fine), and "Out for Delivery" only applies to delivery orders.
    /// </summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to, FulfillmentMethod method)
    {
        if (from is OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Rejected)
            return false;
        if (to == OrderStatus.Cancelled)
            return true;
        if (to == OrderStatus.Rejected)
            return from == OrderStatus.New;
        if (to <= from)
            return false;
        if (to == OrderStatus.OutForDelivery && method == FulfillmentMethod.Pickup)
            return false;
        return true;
    }

    /// <summary>The natural "next step" used for the quick action button in the dashboard.</summary>
    public static OrderStatus? NextStatus(OrderStatus current, FulfillmentMethod method) => current switch
    {
        OrderStatus.New => OrderStatus.Confirmed,
        OrderStatus.Confirmed => OrderStatus.Preparing,
        OrderStatus.Preparing => OrderStatus.Ready,
        OrderStatus.Ready => method == FulfillmentMethod.Delivery
            ? OrderStatus.OutForDelivery
            : OrderStatus.Completed,
        OrderStatus.OutForDelivery => OrderStatus.Completed,
        _ => null
    };
}
