namespace WhatsOrder.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Entities that are hidden instead of physically removed.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
