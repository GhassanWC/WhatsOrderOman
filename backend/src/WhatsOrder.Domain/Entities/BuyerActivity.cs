using WhatsOrder.Domain.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// A lightweight browsing/shopping event used for "recently viewed" and rule-based
/// recommendations. Only shopping signals are recorded — never message contents,
/// addresses, or other sensitive data. UserId is null for anonymous visitors.
/// </summary>
public class BuyerActivity : BaseEntity
{
    public Guid? UserId { get; set; }

    public BuyerEventType EventType { get; set; }

    public Guid? StoreId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CategoryId { get; set; }

    public string? SearchQuery { get; set; }
}
