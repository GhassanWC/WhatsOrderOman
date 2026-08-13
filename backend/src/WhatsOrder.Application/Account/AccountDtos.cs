using FluentValidation;
using WhatsOrder.Application.Orders;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Application.Reviews;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Account;

// ── Profile ────────────────────────────────────────────────────────────────

public sealed record BuyerProfileDto(
    string Email,
    string DisplayName,
    string? Phone,
    string? AvatarUrl,
    string? PreferredLanguage,
    bool NotifyOrderUpdates,
    bool NotifyMessages,
    bool NotifyOffers);

public sealed record UpdateBuyerProfileRequest(
    string DisplayName,
    string? Phone,
    string? PreferredLanguage,
    bool NotifyOrderUpdates = true,
    bool NotifyMessages = true,
    bool NotifyOffers = true);

public class UpdateBuyerProfileRequestValidator : AbstractValidator<UpdateBuyerProfileRequest>
{
    public UpdateBuyerProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.PreferredLanguage)
            .Must(l => l is null or "en" or "ar")
            .WithMessage("PreferredLanguage must be \"en\" or \"ar\".");
    }
}

// ── Addresses ──────────────────────────────────────────────────────────────

public sealed record BuyerAddressDto(
    Guid Id,
    string Label,
    string RecipientName,
    string Phone,
    string? Governorate,
    string? Wilayat,
    string? City,
    string? Area,
    string? Street,
    string? Building,
    string? Apartment,
    string? Notes,
    double? Latitude,
    double? Longitude,
    bool IsDefault,
    DateTime CreatedAt);

public sealed record SaveAddressRequest(
    string Label,
    string RecipientName,
    string Phone,
    string? Governorate,
    string? Wilayat,
    string? City,
    string? Area,
    string? Street,
    string? Building,
    string? Apartment,
    string? Notes,
    double? Latitude,
    double? Longitude,
    bool IsDefault = false);

public class SaveAddressRequestValidator : AbstractValidator<SaveAddressRequest>
{
    public SaveAddressRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(40);
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Governorate).MaximumLength(50);
        RuleFor(x => x.Wilayat).MaximumLength(50);
        RuleFor(x => x.City).MaximumLength(80);
        RuleFor(x => x.Area).MaximumLength(100);
        RuleFor(x => x.Street).MaximumLength(100);
        RuleFor(x => x.Building).MaximumLength(50);
        RuleFor(x => x.Apartment).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(300);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}

// ── Favorites ──────────────────────────────────────────────────────────────

public sealed record FavoriteIdsDto(IReadOnlyList<Guid> StoreIds, IReadOnlyList<Guid> ProductIds);

public sealed record FavoriteStoreDto(
    Guid StoreId,
    string Slug,
    string Name,
    string? NameAr,
    string? LogoUrl,
    string? BannerUrl,
    string? Governorate,
    string? Wilayat,
    bool AcceptingOrders,
    double? Rating,
    int ReviewsCount,
    DateTime SavedAt);

public sealed record FavoriteProductDto(
    Guid ProductId,
    string Name,
    string? NameAr,
    decimal Price,
    decimal? DiscountedPrice,
    string? ImageUrl,
    bool InStock,
    string StoreSlug,
    string StoreName,
    string? StoreNameAr,
    DateTime SavedAt);

public sealed record FavoritesDto(
    IReadOnlyList<FavoriteStoreDto> Stores,
    IReadOnlyList<FavoriteProductDto> Products);

// ── Buyer orders ───────────────────────────────────────────────────────────

public sealed record BuyerOrderListItemDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    FulfillmentMethod FulfillmentMethod,
    decimal Total,
    int ItemsCount,
    string ItemsSummary,
    int UnreadMessages,
    string StoreSlug,
    string StoreName,
    string? StoreNameAr,
    string? StoreLogoUrl,
    bool CanReview,
    DateTime CreatedAt);

public sealed record BuyerOrdersPage(
    IReadOnlyList<BuyerOrderListItemDto> Items, int Total, int Page, int PageSize, int ActiveCount);

public sealed record BuyerOrderDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    FulfillmentMethod FulfillmentMethod,
    string? DeliveryAddress,
    string? PreferredTime,
    string? Notes,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Discount,
    decimal Total,
    List<OrderItemDto> Items,
    DateTime? EstimatedReadyAt,
    List<OrderStatusHistoryDto> StatusHistory,
    int UnreadMessages,
    string StoreSlug,
    string StoreName,
    string? StoreNameAr,
    string? StoreLogoUrl,
    string StoreWhatsAppNumber,
    bool CanReview,
    ReviewDto? Review,
    DateTime CreatedAt);

// ── Conversations ──────────────────────────────────────────────────────────

public sealed record BuyerConversationDto(
    Guid OrderId,
    string OrderNumber,
    string StoreSlug,
    string StoreName,
    string? StoreNameAr,
    string? StoreLogoUrl,
    string LastMessage,
    ChatSender LastSender,
    DateTime LastMessageAt,
    int UnreadCount);

// ── Overview ───────────────────────────────────────────────────────────────

public sealed record AccountOverviewDto(
    BuyerProfileDto Profile,
    int ActiveOrdersCount,
    int UnreadMessages,
    int UnreadNotifications,
    int FavoriteStoresCount,
    int FavoriteProductsCount,
    IReadOnlyList<BuyerOrderListItemDto> RecentOrders,
    BuyerAddressDto? DefaultAddress);
