using WhatsOrder.Domain.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// The store's plan. Billing-provider-ready: external ids are placeholders for
/// Stripe / Lemon Squeezy integration later; the MVP switches plans without payment.
/// </summary>
public class Subscription : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    public string? ExternalCustomerId { get; set; }
    public string? ExternalSubscriptionId { get; set; }
}
