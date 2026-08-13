using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>One choice inside a variant group, e.g. "Large" (+1.500 OMR).</summary>
public class ProductVariantOption : BaseEntity
{
    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }

    /// <summary>Added to (or subtracted from) the product's effective base price.</summary>
    public decimal PriceAdjustment { get; set; }

    public bool IsAvailable { get; set; } = true;
    public int SortOrder { get; set; }
}
