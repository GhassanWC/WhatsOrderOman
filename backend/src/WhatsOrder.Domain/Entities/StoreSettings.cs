using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>1:1 with Store; PK is the StoreId.</summary>
public class StoreSettings
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public decimal DeliveryFee { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public bool DeliveryEnabled { get; set; } = true;
    public bool PickupEnabled { get; set; } = true;

    /// <summary>JSON array of 7 entries: [{"day":0,"closed":false,"open":"09:00","close":"21:00"},…]. day 0 = Sunday.</summary>
    public string OpeningHoursJson { get; set; } = string.Empty;

    public string Currency { get; set; } = "OMR";
    public string DefaultLanguage { get; set; } = "en";
    public string TimeZone { get; set; } = "Asia/Muscat";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
