using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Activity;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Offers;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Public;

/// <summary>Anonymous storefront reads. Only exposes what customers may see.</summary>
public class PublicCatalogService(
    IAppDbContext db,
    OfferService offerService,
    ActivityTracker activity,
    ICurrentUser currentUser)
{
    public async Task<PublicStoreDto> GetStoreAsync(string slug, CancellationToken ct = default)
    {
        var store = await db.Stores
            .Include(s => s.Settings)
            .Include(s => s.Subscription)
            .FirstOrDefaultAsync(s => s.Slug == Slugs.Normalize(slug), ct)
            ?? throw new NotFoundException("Store not found.");

        var categories = await db.Categories
            .Where(c => c.StoreId == store.Id && c.IsActive)
            .Where(c => c.Products.Any(p => !p.IsDeleted && p.IsAvailable))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new PublicCategoryDto(c.Id, c.Name, c.NameAr, c.SortOrder))
            .ToListAsync(ct);

        var acceptingOrders = store.IsAcceptingOrders;
        var limits = PlanCatalog.For(store.Subscription.Plan);
        if (acceptingOrders && limits.MaxOrdersPerMonth is { } maxOrders)
        {
            var monthStartUtc = Orders.OrderService.OmanMonthStartUtc(DateTime.UtcNow);
            var monthCount = await db.Orders.CountAsync(o => o.StoreId == store.Id && o.CreatedAt >= monthStartUtc, ct);
            acceptingOrders = monthCount < maxOrders;
        }

        var hours = OpeningHours.Parse(store.Settings.OpeningHoursJson);

        var ratingStats = await db.Reviews
            .Where(r => r.StoreId == store.Id)
            .GroupBy(r => r.StoreId)
            .Select(g => new { Average = g.Average(r => (double)r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        var runningOffers = await offerService.GetRunningForStoreAsync(store.Id, ct);

        await activity.TrackAsync(BuyerEventType.StoreViewed, currentUser.UserId,
            storeId: store.Id, ct: ct);

        return new PublicStoreDto(
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
            acceptingOrders,
            OpeningHours.IsOpenAt(hours, DateTime.UtcNow),
            hours,
            store.Settings.DeliveryFee,
            store.Settings.MinimumOrderAmount,
            store.Settings.DeliveryEnabled,
            store.Settings.PickupEnabled,
            store.Settings.Currency,
            store.Settings.DefaultLanguage,
            categories,
            ratingStats is null ? null : Math.Round(ratingStats.Average, 1),
            ratingStats?.Count ?? 0,
            runningOffers);
    }

    public async Task<List<PublicProductDto>> GetProductsAsync(
        string slug, string? search, Guid? categoryId, bool? featured, CancellationToken ct = default)
    {
        var storeId = await ResolveStoreIdAsync(slug, ct);

        var query = db.Products
            .Where(p => p.StoreId == storeId && p.IsAvailable);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                (p.NameAr != null && p.NameAr.Contains(term)) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId);
        if (featured == true)
            query = query.Where(p => p.IsFeatured);

        var products = await query
            .Include(p => p.Images)
            .Include(p => p.Variants).ThenInclude(v => v.Options)
            .OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt)
            .Take(500)
            .AsSplitQuery()
            .ToListAsync(ct);

        return products.Select(ToPublicDto).ToList();
    }

    public async Task<PublicProductDto> GetProductAsync(string slug, Guid productId, CancellationToken ct = default)
    {
        var storeId = await ResolveStoreIdAsync(slug, ct);

        var product = await db.Products
            .Include(p => p.Images)
            .Include(p => p.Variants).ThenInclude(v => v.Options)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == productId && p.StoreId == storeId && p.IsAvailable, ct)
            ?? throw new NotFoundException("Product not found.");

        await activity.TrackAsync(BuyerEventType.ProductViewed, currentUser.UserId,
            storeId: storeId, productId: product.Id, ct: ct);

        return ToPublicDto(product);
    }

    private async Task<Guid> ResolveStoreIdAsync(string slug, CancellationToken ct)
    {
        var storeId = await db.Stores
            .Where(s => s.Slug == Slugs.Normalize(slug))
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
        return storeId ?? throw new NotFoundException("Store not found.");
    }

    private static PublicProductDto ToPublicDto(Product p) => new(
        p.Id,
        p.CategoryId,
        p.Name,
        p.NameAr,
        p.Description,
        p.DescriptionAr,
        p.Price,
        p.DiscountedPrice,
        InStock: p.StockQuantity is null or > 0,
        p.IsFeatured,
        p.Images.OrderBy(i => i.SortOrder).Select(i => i.Path).ToList(),
        p.Variants
            .OrderBy(v => v.SortOrder)
            .Select(v => new PublicVariantDto(
                v.Id, v.Name, v.NameAr, v.IsRequired,
                v.Options.Where(o => o.IsAvailable).OrderBy(o => o.SortOrder)
                    .Select(o => new PublicVariantOptionDto(o.Id, o.Name, o.NameAr, o.PriceAdjustment))
                    .ToList()))
            .Where(v => v.Options.Count > 0)
            .ToList());
}
