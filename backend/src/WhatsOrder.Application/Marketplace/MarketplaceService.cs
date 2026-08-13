using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Activity;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Offers;
using WhatsOrder.Application.Recommendations;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Marketplace;

public sealed record StoreDirectoryQuery(
    string? Search,
    string? Governorate,
    bool? Delivery,
    bool? Pickup,
    bool? OpenNow,
    bool? HasOffers,
    double? MinRating,
    string? Sort,
    int Page = 1,
    int PageSize = 12);

/// <summary>Cross-store discovery: the directory, unified search and the homepage.</summary>
public class MarketplaceService(
    IAppDbContext db,
    MarketplaceCardFactory cards,
    IRecommendationService recommendations,
    OfferService offers,
    ActivityTracker activity,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Ranked/derived sorts and filters (rating, popularity, open-now, offers) are
    /// evaluated over a bounded candidate set because they need composed data. The
    /// cap comfortably covers the current store count; at real scale these become
    /// materialized columns and the whole method turns into plain SQL paging.
    /// </summary>
    private const int MaxCandidates = 300;

    public async Task<StoresPage> GetStoresAsync(StoreDirectoryQuery request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 48);
        var sort = (request.Sort ?? "recommended").ToLowerInvariant();

        var query = db.Stores.Include(s => s.Settings).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(term) ||
                s.Slug.Contains(term) ||
                (s.NameAr != null && s.NameAr.Contains(term)) ||
                (s.Description != null && s.Description.ToLower().Contains(term)) ||
                (s.DescriptionAr != null && s.DescriptionAr.Contains(term)) ||
                (s.Wilayat != null && s.Wilayat.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Governorate))
            query = query.Where(s => s.Governorate == request.Governorate.Trim());
        if (request.Delivery == true)
            query = query.Where(s => s.Settings.DeliveryEnabled);
        if (request.Pickup == true)
            query = query.Where(s => s.Settings.PickupEnabled);

        var needsComposedData = request.OpenNow == true || request.HasOffers == true
            || request.MinRating.HasValue || sort is "recommended" or "popular" or "rating";

        if (!needsComposedData)
        {
            query = sort switch
            {
                "newest" => query.OrderByDescending(s => s.CreatedAt),
                "deliveryfee" => query.OrderBy(s => s.Settings.DeliveryFee).ThenBy(s => s.Name),
                "name" => query.OrderBy(s => s.Name),
                _ => query.OrderByDescending(s => s.CreatedAt)
            };

            var total = await query.CountAsync(ct);
            var stores = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new StoresPage(await cards.ComposeStoreCardsAsync(stores, ct), total, page, pageSize);
        }

        var candidates = await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(MaxCandidates)
            .ToListAsync(ct);
        var composed = await cards.ComposeStoreCardsAsync(candidates, ct);

        if (request.OpenNow == true)
            composed = composed.Where(c => c.IsOpenNow && c.AcceptingOrders).ToList();
        if (request.HasOffers == true)
            composed = composed.Where(c => c.HasActiveOffers).ToList();
        if (request.MinRating is { } minRating)
            composed = composed.Where(c => c.Rating >= minRating).ToList();

        var orderCounts = sort is "recommended" or "popular"
            ? await PopularCountsAsync(ct)
            : [];

        composed = sort switch
        {
            "rating" => composed
                .OrderByDescending(c => c.Rating ?? 0).ThenByDescending(c => c.ReviewsCount).ToList(),
            "popular" => composed
                .OrderByDescending(c => orderCounts.GetValueOrDefault(c.Id)).ThenByDescending(c => c.Rating ?? 0).ToList(),
            _ => composed // "recommended"
                .OrderByDescending(c =>
                    orderCounts.GetValueOrDefault(c.Id) * 2.0
                    + (c.Rating ?? 0) * 3.0
                    + (c.HasActiveOffers ? 2 : 0)
                    + (c.IsOpenNow && c.AcceptingOrders ? 1 : 0))
                .ToList()
        };

        var pageItems = composed.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new StoresPage(pageItems, composed.Count, page, pageSize);
    }

    public async Task<SearchResultsDto> SearchAsync(string q, CancellationToken ct = default)
    {
        var term = q.Trim();
        if (term.Length == 0)
            return new SearchResultsDto([], [], [], 0, 0);
        var lower = term.ToLower();

        var storeQuery = db.Stores.Include(s => s.Settings).Where(s =>
            s.Name.ToLower().Contains(lower) ||
            s.Slug.Contains(lower) ||
            (s.NameAr != null && s.NameAr.Contains(term)) ||
            (s.Description != null && s.Description.ToLower().Contains(lower)));
        var storesTotal = await storeQuery.CountAsync(ct);
        var storeEntities = await storeQuery.OrderBy(s => s.Name).Take(8).ToListAsync(ct);

        var productQuery = db.Products.Where(p => p.IsAvailable && (
            p.Name.ToLower().Contains(lower) ||
            (p.NameAr != null && p.NameAr.Contains(term)) ||
            (p.Description != null && p.Description.ToLower().Contains(lower))));
        var productsTotal = await productQuery.CountAsync(ct);
        var products = await cards.ProductCardsAsync(
            productQuery.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt), 24, ct);

        // Grouped in memory: the DISTINCT-inside-group aggregate does not translate.
        var categoryRows = await db.Categories
            .Where(c => c.IsActive && (c.Name.ToLower().Contains(lower)
                                       || (c.NameAr != null && c.NameAr.Contains(term))))
            .Select(c => new { c.Name, c.NameAr, c.StoreId })
            .Take(500)
            .ToListAsync(ct);
        var categories = categoryRows
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CategorySuggestionDto(
                g.Key, g.First().NameAr, g.Select(c => c.StoreId).Distinct().Count()))
            .OrderByDescending(c => c.StoreCount)
            .Take(6)
            .ToList();

        await activity.TrackAsync(BuyerEventType.Searched, currentUser.UserId, searchQuery: term, ct: ct);

        return new SearchResultsDto(
            await cards.ComposeStoreCardsAsync(storeEntities, ct),
            products, categories, storesTotal, productsTotal);
    }

    public async Task<SearchSuggestionsDto> SuggestAsync(string? q, CancellationToken ct = default)
    {
        var term = q?.Trim() ?? "";
        if (term.Length == 0)
        {
            var recent = new List<string>();
            if (currentUser.UserId is { } userId)
            {
                recent = (await db.BuyerActivities
                        .Where(a => a.UserId == userId && a.EventType == BuyerEventType.Searched
                                    && a.SearchQuery != null)
                        .OrderByDescending(a => a.CreatedAt)
                        .Select(a => a.SearchQuery!)
                        .Take(20)
                        .ToListAsync(ct))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToList();
            }

            var since = DateTime.UtcNow.AddDays(-30);
            var popular = await db.BuyerActivities
                .Where(a => a.EventType == BuyerEventType.Searched
                            && a.SearchQuery != null && a.CreatedAt >= since)
                .GroupBy(a => a.SearchQuery!.ToLower())
                .Select(g => new { Query = g.Max(a => a.SearchQuery!), Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(6)
                .Select(g => g.Query)
                .ToListAsync(ct);

            return new SearchSuggestionsDto([], recent, popular);
        }

        var lower = term.ToLower();
        var storeNames = await db.Stores
            .Where(s => s.Name.ToLower().Contains(lower)
                        || (s.NameAr != null && s.NameAr.Contains(term)))
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .Take(4)
            .ToListAsync(ct);
        var productNames = await db.Products
            .Where(p => p.IsAvailable && (p.Name.ToLower().Contains(lower)
                                          || (p.NameAr != null && p.NameAr.Contains(term))))
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .Take(8)
            .ToListAsync(ct);

        var suggestions = storeNames.Concat(productNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        return new SearchSuggestionsDto(suggestions, [], []);
    }

    /// <summary>The homepage: personalized sections for signed-in buyers, generic for visitors.</summary>
    public async Task<MarketplaceHomeDto> GetHomeAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var sections = new List<HomeSectionDto>();

        if (userId is { } user)
        {
            var recentlyViewed = await recommendations.GetRecentlyViewedProductsAsync(user, 8, ct);
            if (recentlyViewed.Count > 0)
                sections.Add(new HomeSectionDto("recentlyViewed", Products: recentlyViewed));

            sections.Add(new HomeSectionDto("recommendedStores",
                Stores: await recommendations.GetRecommendedStoresAsync(user, 8, ct)));
        }

        sections.Add(new HomeSectionDto("popularStores",
            Stores: await recommendations.GetPopularStoresAsync(8, ct)));

        var running = await offers.GetRunningAsync(6, ct);
        if (running.Count > 0)
            sections.Add(new HomeSectionDto("offers", Offers: running));

        sections.Add(new HomeSectionDto("trendingProducts",
            Products: await recommendations.GetTrendingProductsAsync(10, ct)));

        if (userId is { } buyer)
        {
            sections.Add(new HomeSectionDto("recommendedProducts",
                Products: await recommendations.GetRecommendedProductsAsync(buyer, 10, ct)));
        }

        var newStores = await recommendations.GetNewStoresAsync(8, ct);
        if (newStores.Count > 0)
            sections.Add(new HomeSectionDto("newStores", Stores: newStores));

        // Drop empty sections so the client renders only meaningful blocks.
        sections = sections.Where(s =>
            (s.Stores?.Count ?? 0) + (s.Products?.Count ?? 0) + (s.Offers?.Count ?? 0) > 0).ToList();

        return new MarketplaceHomeDto(sections);
    }

    /// <summary>Client-reported activity; only cart/category events are accepted.</summary>
    public async Task TrackClientActivityAsync(TrackActivityRequest request, CancellationToken ct = default)
    {
        var eventType = request.EventType switch
        {
            "AddedToCart" => BuyerEventType.AddedToCart,
            "CategoryViewed" => BuyerEventType.CategoryViewed,
            _ => (BuyerEventType?)null
        };
        if (eventType is null)
            return;

        await activity.TrackAsync(eventType.Value, currentUser.UserId,
            storeId: request.StoreId, productId: request.ProductId,
            categoryId: request.CategoryId, ct: ct);
    }

    private async Task<Dictionary<Guid, int>> PopularCountsAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        return await db.Orders
            .Where(o => o.CreatedAt >= since)
            .GroupBy(o => o.StoreId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
    }
}
