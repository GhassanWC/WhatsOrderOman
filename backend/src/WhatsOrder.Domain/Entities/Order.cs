using WhatsOrder.Domain.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Domain.Entities;

public class Order : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;

    /// <summary>Human-friendly per-store number, e.g. WO-1024. Unique within a store.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>
    /// Marketplace account that placed the order, when the customer was signed in.
    /// Indexed scalar without FK (same pattern as Store.OwnerId) — anonymous orders keep it null.
    /// </summary>
    public Guid? BuyerUserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    /// <summary>E.164 normalized.</summary>
    public string CustomerPhone { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.New;
    public FulfillmentMethod FulfillmentMethod { get; set; }

    public string? DeliveryAddress { get; set; }
    public string? GoogleMapsUrl { get; set; }
    public string? PreferredTime { get; set; }
    public string? Notes { get; set; }

    /// <summary>Optional promise set by the store when accepting ("ready in ~30 min").</summary>
    public DateTime? EstimatedReadyAt { get; set; }

    /// <summary>Sum of line totals (already at discounted prices).</summary>
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    /// <summary>Informational savings vs. original prices.</summary>
    public decimal Discount { get; set; }
    /// <summary>Subtotal + DeliveryFee.</summary>
    public decimal Total { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}
