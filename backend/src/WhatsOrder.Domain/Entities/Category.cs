using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

public class Category : BaseEntity, ISoftDeletable
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
