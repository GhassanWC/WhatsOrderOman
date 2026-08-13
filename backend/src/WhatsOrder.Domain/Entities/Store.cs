using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

public class Store : BaseEntity, ISoftDeletable
{
    public Guid OwnerId { get; set; }

    /// <summary>URL segment, e.g. "alreem" → whatsorder.om/alreem. Lowercase, unique.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public string? LogoPath { get; set; }
    /// <summary>Wide cover image shown on the public store page and marketplace cards.</summary>
    public string? BannerPath { get; set; }

    /// <summary>E.164, e.g. +96891234567. Receives owner notifications; shown on the storefront.</summary>
    public string WhatsAppNumber { get; set; } = string.Empty;
    public string? InstagramHandle { get; set; }

    public string? LocationText { get; set; }
    public string? Governorate { get; set; }
    public string? Wilayat { get; set; }

    public bool IsAcceptingOrders { get; set; } = true;

    /// <summary>Per-store order counter; incremented for every order (WO-1001, WO-1002, …).</summary>
    public int OrderSequence { get; set; } = 1000;

    public bool IsDeleted { get; set; }

    public StoreSettings Settings { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
