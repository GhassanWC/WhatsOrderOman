using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>A variant group on a product, e.g. "Size" or "Flavor".</summary>
public class ProductVariant : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }

    /// <summary>When true the customer must pick exactly one option from this group.</summary>
    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<ProductVariantOption> Options { get; set; } = new List<ProductVariantOption>();
}
