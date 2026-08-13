using Microsoft.EntityFrameworkCore;
using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Common;

/// <summary>Persistence abstraction implemented by the EF Core AppDbContext.</summary>
public interface IAppDbContext
{
    DbSet<Store> Stores { get; }
    DbSet<StoreSettings> StoreSettings { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductVariantOption> ProductVariantOptions { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderStatusHistory> OrderStatusHistory { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<WhatsAppMessage> WhatsAppMessages { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<BuyerProfile> BuyerProfiles { get; }
    DbSet<BuyerAddress> BuyerAddresses { get; }
    DbSet<FavoriteStore> FavoriteStores { get; }
    DbSet<FavoriteProduct> FavoriteProducts { get; }
    DbSet<BuyerActivity> BuyerActivities { get; }
    DbSet<Offer> Offers { get; }
    DbSet<Review> Reviews { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>The authenticated principal of the current request (null for anonymous).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}

/// <summary>
/// Resolves the tenant (store) of the current request from the authenticated user —
/// never from client-supplied ids. Cached per request.
/// </summary>
public interface IStoreContext
{
    Task<Store?> FindMyStoreAsync(CancellationToken ct = default);

    /// <summary>Returns the caller's store or throws <see cref="NotFoundException"/>.</summary>
    Task<Store> GetMyStoreAsync(CancellationToken ct = default);
}

public interface IFileStorage
{
    /// <summary>Saves a file and returns its public relative path (e.g. /uploads/products/x.webp).</summary>
    Task<string> SaveAsync(Stream content, string extension, string subfolder, CancellationToken ct = default);

    Task DeleteAsync(string relativePath, CancellationToken ct = default);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
