using WhatsOrder.Domain.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// A persisted in-app notification for a store owner (customers have no accounts —
/// their "notifications" are the persisted chat + status timeline). Persisting means
/// nothing is lost while the owner is offline; SignalR is delivery only.
/// Scalar ids only (no FKs): orders/stores are never hard-deleted and this table
/// must survive independently of cascade paths.
/// </summary>
public class Notification : BaseEntity
{
    /// <summary>The owner user this notification belongs to.</summary>
    public Guid UserId { get; set; }
    public Guid StoreId { get; set; }
    public Guid? OrderId { get; set; }

    public NotificationType Type { get; set; }

    /// <summary>Short context shown in the notification list, e.g. "#WO-1024 · Ahmed".</summary>
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public bool IsRead { get; set; }
}
