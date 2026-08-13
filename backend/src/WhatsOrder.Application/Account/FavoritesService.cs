using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Activity;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Reviews;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Account;

public class FavoritesService(IAppDbContext db, ICurrentUser currentUser, ActivityTracker activity)
{
    public async Task AddStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        var userId = RequireUser();
        if (!await db.Stores.AnyAsync(s => s.Id == storeId, ct))
            throw new NotFoundException("Store not found.");
        if (await db.FavoriteStores.AnyAsync(f => f.UserId == userId && f.StoreId == storeId, ct))
            return; // Idempotent — the unique index is the backstop for races.

        db.FavoriteStores.Add(new FavoriteStore { UserId = userId, StoreId = storeId });
        await db.SaveChangesAsync(ct);
        await activity.TrackAsync(BuyerEventType.StoreFavorited, userId, storeId: storeId, ct: ct);
    }

    public async Task RemoveStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var favorite = await db.FavoriteStores
            .FirstOrDefaultAsync(f => f.UserId == userId && f.StoreId == storeId, ct);
        if (favorite is null)
            return;
        db.FavoriteStores.Remove(favorite);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddProductAsync(Guid productId, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var product = await db.Products
            .Select(p => new { p.Id, p.StoreId })
            .FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new NotFoundException("Product not found.");
        if (await db.FavoriteProducts.AnyAsync(f => f.UserId == userId && f.ProductId == productId, ct))
            return;

        db.FavoriteProducts.Add(new FavoriteProduct { UserId = userId, ProductId = productId });
        await db.SaveChangesAsync(ct);
        await activity.TrackAsync(BuyerEventType.ProductFavorited, userId,
            storeId: product.StoreId, productId: productId, ct: ct);
    }

    public async Task RemoveProductAsync(Guid productId, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var favorite = await db.FavoriteProducts
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId, ct);
        if (favorite is null)
            return;
        db.FavoriteProducts.Remove(favorite);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Just the ids — the client uses this to decorate hearts everywhere cheaply.</summary>
    public async Task<FavoriteIdsDto> GetIdsAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        var storeIds = await db.FavoriteStores
            .Where(f => f.UserId == userId).Select(f => f.StoreId).ToListAsync(ct);
        var productIds = await db.FavoriteProducts
            .Where(f => f.UserId == userId).Select(f => f.ProductId).ToListAsync(ct);
        return new FavoriteIdsDto(storeIds, productIds);
    }

    public async Task<FavoritesDto> GetAllAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();

        var stores = await db.FavoriteStores
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.StoreId,
                f.Store.Slug,
                f.Store.Name,
                f.Store.NameAr,
                f.Store.LogoPath,
                f.Store.BannerPath,
                f.Store.Governorate,
                f.Store.Wilayat,
                f.Store.IsAcceptingOrders,
                SavedAt = f.CreatedAt
            })
            .ToListAsync(ct);

        var ratings = await ReviewQueries.ForStoresAsync(db, stores.Select(s => s.StoreId).ToList(), ct);

        var products = await db.FavoriteProducts
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.ProductId,
                f.Product.Name,
                f.Product.NameAr,
                f.Product.Price,
                f.Product.DiscountedPrice,
                Image = f.Product.Images
                    .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder)
                    .Select(i => i.Path).FirstOrDefault(),
                InStock = f.Product.IsAvailable &&
                          (f.Product.StockQuantity == null || f.Product.StockQuantity > 0),
                StoreSlug = f.Product.Store.Slug,
                StoreName = f.Product.Store.Name,
                StoreNameAr = f.Product.Store.NameAr,
                SavedAt = f.CreatedAt
            })
            .ToListAsync(ct);

        return new FavoritesDto(
            stores.Select(s => new FavoriteStoreDto(
                s.StoreId, s.Slug, s.Name, s.NameAr, s.LogoPath, s.BannerPath,
                s.Governorate, s.Wilayat, s.IsAcceptingOrders,
                ratings.TryGetValue(s.StoreId, out var r) ? r.Average : null,
                ratings.TryGetValue(s.StoreId, out var r2) ? r2.Count : 0,
                s.SavedAt)).ToList(),
            products.Select(p => new FavoriteProductDto(
                p.ProductId, p.Name, p.NameAr, p.Price, p.DiscountedPrice, p.Image,
                p.InStock, p.StoreSlug, p.StoreName, p.StoreNameAr, p.SavedAt)).ToList());
    }

    private Guid RequireUser() =>
        currentUser.UserId ?? throw new AuthFailedException("Not authenticated.");
}
