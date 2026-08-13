using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>
/// Marketplace-side profile for an authenticated user (one per user, created lazily).
/// Identity fields (email, phone, display name) live on the Identity user; this holds
/// buyer-only preferences and media.
/// </summary>
public class BuyerProfile : BaseEntity
{
    public Guid UserId { get; set; }

    public string? AvatarPath { get; set; }

    /// <summary>"en" or "ar"; null = follow the device/browser.</summary>
    public string? PreferredLanguage { get; set; }

    public bool NotifyOrderUpdates { get; set; } = true;
    public bool NotifyMessages { get; set; } = true;
    public bool NotifyOffers { get; set; } = true;
}
