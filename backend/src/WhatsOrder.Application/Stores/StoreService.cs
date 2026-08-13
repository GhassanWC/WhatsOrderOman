using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Stores;

public class StoreService(IAppDbContext db, ICurrentUser currentUser, IStoreContext storeContext, IFileStorage files)
{
    public async Task<StoreDto> GetMyStoreAsync(CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        return store.ToDto();
    }

    public async Task<StoreDto> CreateStoreAsync(CreateStoreRequest request, CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } userId)
            throw new AuthFailedException("Not authenticated.");

        if (await db.Stores.AnyAsync(s => s.OwnerId == userId, ct))
            throw new ConflictException("You already have a store.");

        var slug = Slugs.Normalize(request.Slug);
        if (!Slugs.IsValid(slug))
            throw new BusinessRuleException("invalid_slug", "This store link is not valid.");
        if (await db.Stores.IgnoreQueryFilters().AnyAsync(s => s.Slug == slug, ct))
            throw new ConflictException("This store link is already taken.");

        // WhatsApp is optional — orders flow through the in-app channel regardless.
        var phone = NormalizeOptionalWhatsApp(request.WhatsAppNumber);

        var store = new Store
        {
            OwnerId = userId,
            Slug = slug,
            Name = request.Name.Trim(),
            NameAr = Clean(request.NameAr),
            Description = Clean(request.Description),
            DescriptionAr = Clean(request.DescriptionAr),
            WhatsAppNumber = phone,
            InstagramHandle = Clean(request.InstagramHandle)?.TrimStart('@'),
            LocationText = Clean(request.LocationText),
            Governorate = Clean(request.Governorate),
            Wilayat = Clean(request.Wilayat),
            IsAcceptingOrders = true
        };
        store.Settings = new StoreSettings
        {
            StoreId = store.Id,
            OpeningHoursJson = OpeningHours.Serialize(OpeningHours.Defaults())
        };
        store.Subscription = new Subscription
        {
            StoreId = store.Id,
            StartsAt = DateTime.UtcNow
        };

        db.Stores.Add(store);
        await db.SaveChangesAsync(ct);
        return store.ToDto();
    }

    public async Task<StoreDto> UpdateStoreAsync(UpdateStoreRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);

        var slug = Slugs.Normalize(request.Slug);
        if (slug != store.Slug)
        {
            if (!Slugs.IsValid(slug))
                throw new BusinessRuleException("invalid_slug", "This store link is not valid.");
            if (await db.Stores.IgnoreQueryFilters().AnyAsync(s => s.Slug == slug && s.Id != store.Id, ct))
                throw new ConflictException("This store link is already taken.");
            store.Slug = slug;
        }

        store.Name = request.Name.Trim();
        store.NameAr = Clean(request.NameAr);
        store.Description = Clean(request.Description);
        store.DescriptionAr = Clean(request.DescriptionAr);
        store.WhatsAppNumber = NormalizeOptionalWhatsApp(request.WhatsAppNumber);
        store.InstagramHandle = Clean(request.InstagramHandle)?.TrimStart('@');
        store.LocationText = Clean(request.LocationText);
        store.Governorate = Clean(request.Governorate);
        store.Wilayat = Clean(request.Wilayat);
        store.IsAcceptingOrders = request.IsAcceptingOrders;

        await db.SaveChangesAsync(ct);
        return store.ToDto();
    }

    public async Task<StoreDto> UpdateSettingsAsync(UpdateStoreSettingsRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);

        store.Settings.DeliveryFee = Money.Round(request.DeliveryFee);
        store.Settings.MinimumOrderAmount = Money.Round(request.MinimumOrderAmount);
        store.Settings.DeliveryEnabled = request.DeliveryEnabled;
        store.Settings.PickupEnabled = request.PickupEnabled;
        store.Settings.OpeningHoursJson = OpeningHours.Serialize(request.OpeningHours);
        store.Settings.DefaultLanguage = request.DefaultLanguage;

        await db.SaveChangesAsync(ct);
        return store.ToDto();
    }

    public async Task<StoreDto> SetLogoAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);

        var oldLogo = store.LogoPath;
        store.LogoPath = await files.SaveAsync(content, extension, "logos", ct);
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(oldLogo))
            await files.DeleteAsync(oldLogo, ct);

        return store.ToDto();
    }

    public async Task<StoreDto> SetBannerAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);

        var oldBanner = store.BannerPath;
        store.BannerPath = await files.SaveAsync(content, extension, "banners", ct);
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(oldBanner))
            await files.DeleteAsync(oldBanner, ct);

        return store.ToDto();
    }

    public async Task<StoreDto> RemoveLogoAsync(CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var oldLogo = store.LogoPath;
        store.LogoPath = null;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(oldLogo))
            await files.DeleteAsync(oldLogo, ct);

        return store.ToDto();
    }

    public async Task<StoreDto> RemoveBannerAsync(CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var oldBanner = store.BannerPath;
        store.BannerPath = null;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(oldBanner))
            await files.DeleteAsync(oldBanner, ct);

        return store.ToDto();
    }

    public async Task<SlugAvailabilityDto> CheckSlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = Slugs.Normalize(slug);
        var isValid = Slugs.IsValid(normalized);
        var mine = await storeContext.FindMyStoreAsync(ct);
        var taken = isValid && await db.Stores.IgnoreQueryFilters()
            .AnyAsync(s => s.Slug == normalized && (mine == null || s.Id != mine.Id), ct);
        return new SlugAvailabilityDto(normalized, isValid, isValid && !taken);
    }

    /// <summary>Empty stays empty (WhatsApp not connected); anything else must be a valid number.</summary>
    private static string NormalizeOptionalWhatsApp(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        return PhoneNumber.Normalize(input)
            ?? throw new BusinessRuleException("invalid_phone", "Invalid WhatsApp number.");
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
