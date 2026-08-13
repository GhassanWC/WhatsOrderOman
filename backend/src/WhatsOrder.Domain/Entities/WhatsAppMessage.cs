using WhatsOrder.Domain.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Domain.Entities;

/// <summary>Log of every WhatsApp message we send or receive, with delivery state.</summary>
public class WhatsAppMessage : BaseEntity
{
    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }

    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    public WhatsAppDirection Direction { get; set; }
    public WhatsAppMessageType Type { get; set; }

    /// <summary>Counterparty phone in E.164 (recipient for outbound, sender for inbound).</summary>
    public string Phone { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Meta message id ("wamid...") used to correlate webhook status updates.</summary>
    public string? WaMessageId { get; set; }

    public WhatsAppMessageStatus Status { get; set; } = WhatsAppMessageStatus.Pending;
    public string? Error { get; set; }
}
