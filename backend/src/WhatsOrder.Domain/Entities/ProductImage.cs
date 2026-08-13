using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Relative path served by the API, e.g. /uploads/products/xxx.webp</summary>
    public string Path { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}
