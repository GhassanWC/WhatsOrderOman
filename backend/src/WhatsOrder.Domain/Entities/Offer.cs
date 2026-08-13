using WhatsOrder.Domain.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// A store-wide promotion. Applied automatically at checkout when the order subtotal
/// meets <see cref="MinimumOrderAmount"/> and the offer is currently running.
/// Designed so coupon codes can be added later (a nullable Code column + lookup).
/// </summary>
public class Offer : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }

    public OfferType Type { get; set; }

    /// <summary>Percentage (0–100) for <see cref="OfferType.Percentage"/>, OMR amount for FixedAmount; unused for FreeDelivery.</summary>
    public decimal DiscountValue { get; set; }

    /// <summary>Order subtotal required before the offer applies. 0 = no minimum.</summary>
    public decimal MinimumOrderAmount { get; set; }

    public DateTime StartsAt { get; set; }
    /// <summary>Null = no end date.</summary>
    public DateTime? EndsAt { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>True when the offer is enabled and inside its schedule window.</summary>
    public bool IsRunningAt(DateTime utcNow) =>
        IsActive && StartsAt <= utcNow && (EndsAt is null || EndsAt > utcNow);
}
