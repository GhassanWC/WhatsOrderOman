using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>A store saved by a buyer. Unique per (UserId, StoreId).</summary>
public class FavoriteStore : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;
}

/// <summary>A product saved by a buyer. Unique per (UserId, ProductId).</summary>
public class FavoriteProduct : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
