using WhatsOrder.Domain.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// Immutable audit trail of an order's status changes; also powers the customer's
/// status timeline. ChangedAt = CreatedAt from BaseEntity.
/// </summary>
public class OrderStatusHistory : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    /// <summary>Null for the initial "order placed" entry.</summary>
    public OrderStatus? PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }

    /// <summary>"customer" | "store" | "system".</summary>
    public string ChangedBy { get; set; } = string.Empty;
}
