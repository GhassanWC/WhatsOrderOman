using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Stores;

public sealed record StoreSettingsDto(
    decimal DeliveryFee,
    decimal MinimumOrderAmount,
    bool DeliveryEnabled,
    bool PickupEnabled,
    List<OpeningHourItem> OpeningHours,
    string DefaultLanguage,
    string Currency);

public sealed record StoreDto(
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
    bool IsAcceptingOrders,
    StoreSettingsDto Settings,
    string Plan,
    DateTime CreatedAt);

public sealed record CreateStoreRequest(
    string Name,
    string? NameAr,
    string Slug,
    string? WhatsAppNumber,
    string? InstagramHandle,
    string? Description,
    string? DescriptionAr,
    string? LocationText,
    string? Governorate,
    string? Wilayat);

public sealed record UpdateStoreRequest(
    string Name,
    string? NameAr,
    string Slug,
    string? WhatsAppNumber,
    string? InstagramHandle,
    string? Description,
    string? DescriptionAr,
    string? LocationText,
    string? Governorate,
    string? Wilayat,
    bool IsAcceptingOrders);

public sealed record UpdateStoreSettingsRequest(
    decimal DeliveryFee,
    decimal MinimumOrderAmount,
    bool DeliveryEnabled,
    bool PickupEnabled,
    List<OpeningHourItem> OpeningHours,
    string DefaultLanguage);

public sealed record SlugAvailabilityDto(string Slug, bool IsValid, bool IsAvailable);

public static class StoreMapping
{
    public static StoreDto ToDto(this Store store) => new(
        store.Id,
        store.Slug,
        store.Name,
        store.NameAr,
        store.Description,
        store.DescriptionAr,
        store.LogoPath,
        store.BannerPath,
        store.WhatsAppNumber,
        store.InstagramHandle,
        store.LocationText,
        store.Governorate,
        store.Wilayat,
        store.IsAcceptingOrders,
        new StoreSettingsDto(
            store.Settings.DeliveryFee,
            store.Settings.MinimumOrderAmount,
            store.Settings.DeliveryEnabled,
            store.Settings.PickupEnabled,
            OpeningHours.Parse(store.Settings.OpeningHoursJson),
            store.Settings.DefaultLanguage,
            store.Settings.Currency),
        store.Subscription.Plan.ToString(),
        store.CreatedAt);
}
