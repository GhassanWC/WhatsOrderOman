using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// Immutable snapshot of a purchased line. Product name, variant text and prices are
/// copied at order time so later product edits never change historical orders.
/// </summary>
public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;

    /// <summary>e.g. "Size: Large • Flavor: Chocolate" (localized snapshot).</summary>
    public string? VariantsText { get; set; }

    /// <summary>Charged unit price incl. variant adjustments and discounts.</summary>
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
