using WhatsOrder.Domain.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// One message in an order's conversation. The conversation IS the order (strictly 1:1),
/// so OrderId doubles as the conversation key — no separate conversation table.
/// SentAt = CreatedAt from BaseEntity.
/// </summary>
public class ChatMessage : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    /// <summary>Denormalized for store-wide unread queries; no FK to avoid a second cascade path.</summary>
    public Guid StoreId { get; set; }

    public ChatSender Sender { get; set; }

    /// <summary>Free text for Customer/Store; a machine token (e.g. "status:Confirmed") for System.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>When the counterparty loaded this message. System messages are born read.</summary>
    public DateTime? ReadAt { get; set; }
}
