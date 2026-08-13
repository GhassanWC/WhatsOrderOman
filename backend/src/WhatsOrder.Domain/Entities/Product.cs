using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

public class Product : BaseEntity, ISoftDeletable
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }

    /// <summary>Base price in OMR (3 decimal places).</summary>
    public decimal Price { get; set; }

    /// <summary>Optional promotional price; when set, this is the charged base price.</summary>
    public decimal? DiscountedPrice { get; set; }

    /// <summary>null = stock not tracked (always purchasable while available).</summary>
    public int? StockQuantity { get; set; }

    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

    public decimal EffectiveBasePrice => DiscountedPrice ?? Price;
    public bool TracksStock => StockQuantity.HasValue;
}
