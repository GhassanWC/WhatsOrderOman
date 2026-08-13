using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Marketplace;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Recommendations;

/// <summary>
/// Deterministic, explainable ranking from stored signals: the buyer's favorites,
/// orders and views, plus marketplace-wide popularity. No per-user model state —
/// every score is recomputed from the last few weeks of data on each call.
/// </summary>
public class RecommendationService(IAppDbContext db, MarketplaceCardFactory cards) : IRecommendationService
{
    private static readonly TimeSpan PopularWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan TrendingWindow = TimeSpan.FromDays(14);
    private static readonly TimeSpan PersonalWindow = TimeSpan.FromDays(60);

    // Signal weights — tuned for "obviously sensible", not optimality.
    private const double FavoriteStoreWeight = 5.0;
    private const double OrderedFromStoreWeight = 3.0;
    private const double ViewedStoreWeight = 1.5;
    private const double ProductSignalStoreWeight = 1.0;
    private const double PopularityWeight = 0.5;

    public async Task<List<StoreCardDto>> GetRecommendedStoresAsync(
        Guid userId, int count, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - PersonalWindow;
        var scores = new Dictionary<Guid, double>();

        void Bump(Guid storeId, double weight) =>
            scores[storeId] = scores.GetValueOrDefault(storeId) + weight;

        var favoriteStores = await db.FavoriteStores
            .Where(f => f.UserId == userId).Select(f => f.StoreId).ToListAsync(ct);
        foreach (var id in favoriteStores)
            Bump(id, FavoriteStoreWeight);

        var orderedCounts = await db.Orders
            .Where(o => o.BuyerUserId == userId && o.CreatedAt >= since)
            .GroupBy(o => o.StoreId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        foreach (var row in orderedCounts)
            Bump(row.Key, OrderedFromStoreWeight * Math.Min(row.Count, 3));

        var activity = await db.BuyerActivities
            .Where(a => a.UserId == userId && a.CreatedAt >= since && a.StoreId != null)
            .GroupBy(a => new { a.StoreId, a.EventType })
            .Select(g => new { g.Key.StoreId, g.Key.EventType, Count = g.Count() })
            .ToListAsync(ct);
        foreach (var row in activity)
        {
            var weight = row.EventType switch
            {
                BuyerEventType.StoreViewed => ViewedStoreWeight,
                BuyerEventType.ProductViewed => ProductSignalStoreWeight,
                BuyerEventType.AddedToCart => ProductSignalStoreWeight * 2,
                _ => 0
            };
            if (weight > 0)
                Bump(row.StoreId!.Value, weight * Math.Min(row.Count, 4));
        }

        // Blend in global popularity so strong stores surface even with thin history.
        foreach (var (storeId, orders) in await PopularStoreCountsAsync(ct))
            Bump(storeId, PopularityWeight * Math.Min(orders, 10) / 10.0);

        var rankedIds = scores.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(count).ToList();
        var result = await cards.StoreCardsByIdsAsync(rankedIds, ct);
        return await FillStoresAsync(result, count, ct);
    }

    public async Task<List<ProductCardDto>> GetRecommendedProductsAsync(
        Guid userId, int count, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - PersonalWindow;
        var scores = new Dictionary<Guid, double>();

        void Bump(Guid productId, double weight) =>
            scores[productId] = scores.GetValueOrDefault(productId) + weight;

        var favoriteProducts = await db.FavoriteProducts
            .Where(f => f.UserId == userId).Select(f => f.ProductId).ToListAsync(ct);
        foreach (var id in favoriteProducts)
            Bump(id, 4.0);

        var viewedOrCarted = await db.BuyerActivities
            .Where(a => a.UserId == userId && a.CreatedAt >= since && a.ProductId != null
                        && (a.EventType == BuyerEventType.ProductViewed
                            || a.EventType == BuyerEventType.AddedToCart))
            .GroupBy(a => a.ProductId)
            .Select(g => new { g.Key, Count = g.Count(), Latest = g.Max(a => a.CreatedAt) })
            .OrderByDescending(g => g.Latest)
            .Take(50)
            .ToListAsync(ct);
        foreach (var row in viewedOrCarted)
            Bump(row.Key!.Value, 2.0 * Math.Min(row.Count, 3));

        // Featured products from stores the buyer favorited or ordered from.
        var affinityStores = await db.FavoriteStores
            .Where(f => f.UserId == userId).Select(f => f.StoreId)
            .Union(db.Orders.Where(o => o.BuyerUserId == userId).Select(o => o.StoreId))
            .ToListAsync(ct);
        if (affinityStores.Count > 0)
        {
            var featured = await db.Products
                .Where(p => affinityStores.Contains(p.StoreId) && p.IsAvailable && p.IsFeatured)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => p.Id)
                .Take(20)
                .ToListAsync(ct);
            foreach (var id in featured)
                Bump(id, 1.5);
        }

        var rankedIds = scores.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(count).ToList();
        var result = await cards.ProductCardsByIdsAsync(rankedIds, ct);
        return await FillProductsAsync(result, count, ct);
    }

    public async Task<List<StoreCardDto>> GetPopularStoresAsync(int count, CancellationToken ct = default)
    {
        var rankedIds = (await PopularStoreCountsAsync(ct))
            .OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(count).ToList();
        var result = await cards.StoreCardsByIdsAsync(rankedIds, ct);
        return await FillStoresAsync(result, count, ct);
    }

    public async Task<List<ProductCardDto>> GetTrendingProductsAsync(int count, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - TrendingWindow;
        var rankedIds = await db.OrderItems
            .Where(i => i.ProductId != null && i.Order.CreatedAt >= since)
            .GroupBy(i => i.ProductId!.Value)
            .Select(g => new { g.Key, Quantity = g.Sum(i => i.Quantity) })
            .OrderByDescending(g => g.Quantity)
            .Select(g => g.Key)
            .Take(count)
            .ToListAsync(ct);

        var result = await cards.ProductCardsByIdsAsync(rankedIds, ct);
        return await FillProductsAsync(result, count, ct);
    }

    public async Task<List<StoreCardDto>> GetNewStoresAsync(int count, CancellationToken ct = default)
    {
        var ids = await db.Stores
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.Id)
            .Take(count)
            .ToListAsync(ct);
        return await cards.StoreCardsByIdsAsync(ids, ct);
    }

    public async Task<List<ProductCardDto>> GetRecentlyViewedProductsAsync(
        Guid userId, int count, CancellationToken ct = default)
    {
        var ids = await RecentDistinctAsync(
            db.BuyerActivities.Where(a =>
                a.UserId == userId && a.EventType == BuyerEventType.ProductViewed && a.ProductId != null),
            a => a.ProductId!.Value, count, ct);
        return await cards.ProductCardsByIdsAsync(ids, ct);
    }

    public async Task<List<StoreCardDto>> GetRecentlyViewedStoresAsync(
        Guid userId, int count, CancellationToken ct = default)
    {
        var ids = await RecentDistinctAsync(
            db.BuyerActivities.Where(a =>
                a.UserId == userId && a.EventType == BuyerEventType.StoreViewed && a.StoreId != null),
            a => a.StoreId!.Value, count, ct);
        return await cards.StoreCardsByIdsAsync(ids, ct);
    }

    public async Task<List<ProductCardDto>> GetRelatedProductsAsync(
        Guid productId, int count, CancellationToken ct = default)
    {
        var product = await db.Products
            .Select(p => new { p.Id, p.StoreId, p.CategoryId })
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
            return [];

        var related = await cards.ProductCardsAsync(
            db.Products
                .Where(p => p.StoreId == product.StoreId && p.Id != product.Id)
                .OrderByDescending(p => p.CategoryId == product.CategoryId)
                .ThenByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.CreatedAt),
            count, ct);
        return related;
    }

    public async Task<List<ProductCardDto>> GetPopularStoreProductsAsync(
        Guid storeId, int count, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - PopularWindow;
        var rankedIds = await db.OrderItems
            .Where(i => i.ProductId != null && i.Order.StoreId == storeId && i.Order.CreatedAt >= since)
            .GroupBy(i => i.ProductId!.Value)
            .Select(g => new { g.Key, Quantity = g.Sum(i => i.Quantity) })
            .OrderByDescending(g => g.Quantity)
            .Select(g => g.Key)
            .Take(count)
            .ToListAsync(ct);

        var result = await cards.ProductCardsByIdsAsync(rankedIds, ct);
        if (result.Count >= count)
            return result;

        var have = result.Select(p => p.Id).ToHashSet();
        var fill = await cards.ProductCardsAsync(
            db.Products
                .Where(p => p.StoreId == storeId && !have.Contains(p.Id))
                .OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt),
            count - result.Count, ct);
        result.AddRange(fill);
        return result;
    }

    /// <summary>Orders per store inside the popularity window.</summary>
    private async Task<Dictionary<Guid, int>> PopularStoreCountsAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow - PopularWindow;
        return await db.Orders
            .Where(o => o.CreatedAt >= since)
            .GroupBy(o => o.StoreId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
    }

    private static async Task<List<Guid>> RecentDistinctAsync<T>(
        IQueryable<T> query, System.Linq.Expressions.Expression<Func<T, Guid>> selector,
        int count, CancellationToken ct) where T : Domain.Entities.BuyerActivity
    {
        var recent = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(selector)
            .Take(count * 4)
            .ToListAsync(ct);
        return recent.Distinct().Take(count).ToList();
    }

    /// <summary>Tops up a ranked list with newest stores so sections never look empty.</summary>
    private async Task<List<StoreCardDto>> FillStoresAsync(
        List<StoreCardDto> ranked, int count, CancellationToken ct)
    {
        if (ranked.Count >= count)
            return ranked;

        var have = ranked.Select(s => s.Id).ToHashSet();
        var fillIds = await db.Stores
            .Where(s => !have.Contains(s.Id))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.Id)
            .Take(count - ranked.Count)
            .ToListAsync(ct);
        ranked.AddRange(await cards.StoreCardsByIdsAsync(fillIds, ct));
        return ranked;
    }

    /// <summary>Tops up a ranked list with featured/newest products.</summary>
    private async Task<List<ProductCardDto>> FillProductsAsync(
        List<ProductCardDto> ranked, int count, CancellationToken ct)
    {
        if (ranked.Count >= count)
            return ranked;

        var have = ranked.Select(p => p.Id).ToHashSet();
        var fill = await cards.ProductCardsAsync(
            db.Products
                .Where(p => !have.Contains(p.Id))
                .OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt),
            count - ranked.Count, ct);
        ranked.AddRange(fill);
        return ranked;
    }
}
