using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>A saved delivery address belonging to a buyer account.</summary>
public class BuyerAddress : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Short label such as "Home" or "Work".</summary>
    public string Label { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;
    /// <summary>E.164 normalized.</summary>
    public string Phone { get; set; } = string.Empty;

    public string? Governorate { get; set; }
    public string? Wilayat { get; set; }
    public string? City { get; set; }
    public string? Area { get; set; }
    public string? Street { get; set; }
    public string? Building { get; set; }
    public string? Apartment { get; set; }
    public string? Notes { get; set; }

    // Doubles (not the OMR decimal convention) so map coordinates keep full precision.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public bool IsDefault { get; set; }
}
