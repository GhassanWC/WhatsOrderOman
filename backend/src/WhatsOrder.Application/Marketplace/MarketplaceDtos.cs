using WhatsOrder.Application.Offers;

namespace WhatsOrder.Application.Marketplace;

/// <summary>A store as shown on marketplace cards (directory, search, recommendations).</summary>
public sealed record StoreCardDto(
    Guid Id,
    string Slug,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? LogoUrl,
    string? BannerUrl,
    string? Governorate,
    string? Wilayat,
    bool AcceptingOrders,
    bool IsOpenNow,
    decimal DeliveryFee,
    decimal MinimumOrderAmount,
    bool DeliveryEnabled,
    bool PickupEnabled,
    double? Rating,
    int ReviewsCount,
    bool HasActiveOffers,
    bool IsNew);

/// <summary>A product as shown on cross-store cards (search, trending, recommendations).</summary>
public sealed record ProductCardDto(
    Guid Id,
    string Name,
    string? NameAr,
    decimal Price,
    decimal? DiscountedPrice,
    string? ImageUrl,
    bool InStock,
    bool IsFeatured,
    string StoreSlug,
    string StoreName,
    string? StoreNameAr);

public sealed record StoresPage(IReadOnlyList<StoreCardDto> Items, int Total, int Page, int PageSize);

public sealed record CategorySuggestionDto(string Name, string? NameAr, int StoreCount);

public sealed record SearchResultsDto(
    List<StoreCardDto> Stores,
    List<ProductCardDto> Products,
    List<CategorySuggestionDto> Categories,
    int StoresTotal,
    int ProductsTotal);

/// <summary>Recent is only filled for signed-in users; Popular comes from everyone's searches.</summary>
public sealed record SearchSuggestionsDto(
    List<string> Suggestions,
    List<string> Recent,
    List<string> Popular);

/// <summary>One homepage block. Exactly one of Stores/Products/Offers is set, matching the Key.</summary>
public sealed record HomeSectionDto(
    string Key,
    List<StoreCardDto>? Stores = null,
    List<ProductCardDto>? Products = null,
    List<PublicOfferDto>? Offers = null);

public sealed record MarketplaceHomeDto(List<HomeSectionDto> Sections);

/// <summary>Client-reported events; only cart/category events are accepted from the client.</summary>
public sealed record TrackActivityRequest(
    string EventType, Guid? StoreId, Guid? ProductId, Guid? CategoryId);
