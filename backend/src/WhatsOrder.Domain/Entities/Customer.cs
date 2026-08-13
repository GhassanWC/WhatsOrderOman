using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// A per-store customer record keyed by phone number. Customers never authenticate;
/// this exists so owners can see repeat customers and totals.
/// </summary>
public class Customer : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>E.164 normalized, e.g. +96891234567.</summary>
    public string Phone { get; set; } = string.Empty;

    public int OrdersCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? FirstOrderAt { get; set; }
    public DateTime? LastOrderAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
