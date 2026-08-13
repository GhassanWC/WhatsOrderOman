using WhatsOrder.Application.Marketplace;

namespace WhatsOrder.Application.Recommendations;

/// <summary>
/// Ranks stores and products for discovery surfaces. The first implementation is
/// deterministic and rule-based; controllers and the Angular client only depend on
/// this interface, so a learned ranker can replace it without touching either.
/// </summary>
public interface IRecommendationService
{
    Task<List<StoreCardDto>> GetRecommendedStoresAsync(Guid userId, int count, CancellationToken ct = default);
    Task<List<ProductCardDto>> GetRecommendedProductsAsync(Guid userId, int count, CancellationToken ct = default);
    Task<List<StoreCardDto>> GetPopularStoresAsync(int count, CancellationToken ct = default);
    Task<List<ProductCardDto>> GetTrendingProductsAsync(int count, CancellationToken ct = default);
    Task<List<StoreCardDto>> GetNewStoresAsync(int count, CancellationToken ct = default);
    Task<List<ProductCardDto>> GetRecentlyViewedProductsAsync(Guid userId, int count, CancellationToken ct = default);
    Task<List<StoreCardDto>> GetRecentlyViewedStoresAsync(Guid userId, int count, CancellationToken ct = default);

    /// <summary>Same-store companions for a product details page.</summary>
    Task<List<ProductCardDto>> GetRelatedProductsAsync(Guid productId, int count, CancellationToken ct = default);

    /// <summary>Popularity ranking of a store's own products (order counts, then featured).</summary>
    Task<List<ProductCardDto>> GetPopularStoreProductsAsync(Guid storeId, int count, CancellationToken ct = default);
}
