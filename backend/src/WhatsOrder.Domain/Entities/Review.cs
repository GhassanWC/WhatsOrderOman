using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// A store review left by the buyer of a completed order. One review per order.
/// StoreId is denormalized (no FK) to avoid a second cascade path Store→Review
/// alongside Store→Order→Review — same pattern as ChatMessage.
/// </summary>
public class Review : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid StoreId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>1–5 stars.</summary>
    public int Rating { get; set; }
    public string? Comment { get; set; }

    /// <summary>Display name captured at review time so listings need no Identity join.</summary>
    public string ReviewerName { get; set; } = string.Empty;
}
