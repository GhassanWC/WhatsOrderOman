using WhatsOrder.Application.Common;
using WhatsOrder.Application.Offers;

namespace WhatsOrder.Application.Public;

public sealed record PublicCategoryDto(Guid Id, string Name, string? NameAr, int SortOrder);

public sealed record PublicStoreDto(
    Guid Id,
    string Slug,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? LogoUrl,
    string? BannerUrl,
    string WhatsAppNumber,
    string? InstagramHandle,
    string? LocationText,
    string? Governorate,
    string? Wilayat,
    bool AcceptingOrders,
    bool IsOpenNow,
    List<OpeningHourItem> OpeningHours,
    decimal DeliveryFee,
    decimal MinimumOrderAmount,
    bool DeliveryEnabled,
    bool PickupEnabled,
    string Currency,
    string DefaultLanguage,
    List<PublicCategoryDto> Categories,
    double? Rating,
    int ReviewsCount,
    List<PublicOfferDto> Offers);

public sealed record PublicVariantOptionDto(Guid Id, string Name, string? NameAr, decimal PriceAdjustment);

public sealed record PublicVariantDto(
    Guid Id, string Name, string? NameAr, bool IsRequired, List<PublicVariantOptionDto> Options);

public sealed record PublicProductDto(
    Guid Id,
    Guid? CategoryId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    decimal Price,
    decimal? DiscountedPrice,
    bool InStock,
    bool IsFeatured,
    List<string> Images,
    List<PublicVariantDto> Variants);
