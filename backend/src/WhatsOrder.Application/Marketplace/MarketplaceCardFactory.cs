using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Reviews;
using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Marketplace;

/// <summary>
/// Composes store/product cards with their aggregate decorations (rating, active
/// offers, open-now) in a fixed number of queries per batch — never per card.
/// </summary>
public class MarketplaceCardFactory(IAppDbContext db)
{
    private static readonly TimeSpan NewStoreWindow = TimeSpan.FromDays(30);

    /// <summary>Cards for the given ids, preserving the input order (ranking order).</summary>
    public async Task<List<StoreCardDto>> StoreCardsByIdsAsync(
        IReadOnlyList<Guid> storeIds, CancellationToken ct = default)
    {
        if (storeIds.Count == 0)
            return [];

        var stores = await db.Stores
            .Include(s => s.Settings)
            .Where(s => storeIds.Contains(s.Id))
            .ToListAsync(ct);

        var cards = await ComposeStoreCardsAsync(stores, ct);
        var byId = cards.ToDictionary(c => c.Id);
        return storeIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    /// <summary>Decorates already-loaded stores (Settings must be included).</summary>
    public async Task<List<StoreCardDto>> ComposeStoreCardsAsync(
        IReadOnlyList<Store> stores, CancellationToken ct = default)
    {
        if (stores.Count == 0)
            return [];

        var ids = stores.Select(s => s.Id).ToList();
        var now = DateTime.UtcNow;

        var ratings = await ReviewQueries.ForStoresAsync(db, ids, ct);
        var offerStoreIds = (await db.Offers
            .Where(o => ids.Contains(o.StoreId) && o.IsActive
                        && o.StartsAt <= now && (o.EndsAt == null || o.EndsAt > now))
            .Select(o => o.StoreId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        return stores.Select(s =>
        {
            var rating = ratings.TryGetValue(s.Id, out var r) ? r : null;
            return new StoreCardDto(
                s.Id, s.Slug, s.Name, s.NameAr, s.Description, s.DescriptionAr,
                s.LogoPath, s.BannerPath, s.Governorate, s.Wilayat,
                s.IsAcceptingOrders,
                OpeningHours.IsOpenAt(OpeningHours.Parse(s.Settings.OpeningHoursJson), now),
                s.Settings.DeliveryFee, s.Settings.MinimumOrderAmount,
                s.Settings.DeliveryEnabled, s.Settings.PickupEnabled,
                rating?.Average, rating?.Count ?? 0,
                offerStoreIds.Contains(s.Id),
                now - s.CreatedAt < NewStoreWindow);
        }).ToList();
    }

    /// <summary>Cards for the given ids, preserving order. Unavailable products are dropped.</summary>
    public async Task<List<ProductCardDto>> ProductCardsByIdsAsync(
        IReadOnlyList<Guid> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0)
            return [];

        var cards = await ProductCardsAsync(
            db.Products.Where(p => productIds.Contains(p.Id)), int.MaxValue, ct);
        var byId = cards.ToDictionary(c => c.Id);
        return productIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    /// <summary>Projects available products of the query into cards (store data included).</summary>
    public async Task<List<ProductCardDto>> ProductCardsAsync(
        IQueryable<Product> query, int take, CancellationToken ct = default)
    {
        var rows = await query
            .Where(p => p.IsAvailable)
            .Select(p => new
            {
                p.Id, p.Name, p.NameAr, p.Price, p.DiscountedPrice, p.IsFeatured,
                Image = p.Images
                    .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
                    .Select(i => i.Path).FirstOrDefault(),
                InStock = p.StockQuantity == null || p.StockQuantity > 0,
                StoreSlug = p.Store.Slug,
                StoreName = p.Store.Name,
                StoreNameAr = p.Store.NameAr
            })
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(r => new ProductCardDto(
            r.Id, r.Name, r.NameAr, r.Price, r.DiscountedPrice, r.Image,
            r.InStock, r.IsFeatured, r.StoreSlug, r.StoreName, r.StoreNameAr)).ToList();
    }
}
