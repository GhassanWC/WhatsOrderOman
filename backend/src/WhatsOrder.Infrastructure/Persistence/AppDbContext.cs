using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Common;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Infrastructure.Identity;

namespace WhatsOrder.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantOption> ProductVariantOptions => Set<ProductVariantOption>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<BuyerProfile> BuyerProfiles => Set<BuyerProfile>();
    public DbSet<BuyerAddress> BuyerAddresses => Set<BuyerAddress>();
    public DbSet<FavoriteStore> FavoriteStores => Set<FavoriteStore>();
    public DbSet<FavoriteProduct> FavoriteProducts => Set<FavoriteProduct>();
    public DbSet<BuyerActivity> BuyerActivities => Set<BuyerActivity>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // OMR uses 3 decimal places (baisa).
        configurationBuilder.Properties<decimal>().HavePrecision(12, 3);
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(100);
        });

        builder.Entity<Store>(e =>
        {
            e.HasIndex(s => s.Slug).IsUnique();
            e.HasIndex(s => s.OwnerId).IsUnique();
            e.Property(s => s.Slug).HasMaxLength(40);
            e.Property(s => s.Name).HasMaxLength(100);
            e.Property(s => s.NameAr).HasMaxLength(100);
            e.Property(s => s.Description).HasMaxLength(500);
            e.Property(s => s.DescriptionAr).HasMaxLength(500);
            e.Property(s => s.LogoPath).HasMaxLength(300);
            e.Property(s => s.BannerPath).HasMaxLength(300);
            e.Property(s => s.WhatsAppNumber).HasMaxLength(20);
            e.Property(s => s.InstagramHandle).HasMaxLength(50);
            e.Property(s => s.LocationText).HasMaxLength(200);
            e.Property(s => s.Governorate).HasMaxLength(50);
            e.Property(s => s.Wilayat).HasMaxLength(50);
            e.HasQueryFilter(s => !s.IsDeleted);

            e.HasOne(s => s.Settings).WithOne(x => x.Store)
                .HasForeignKey<StoreSettings>(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Subscription).WithOne(x => x.Store)
                .HasForeignKey<Subscription>(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StoreSettings>(e =>
        {
            e.HasKey(x => x.StoreId);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.DefaultLanguage).HasMaxLength(5);
            e.Property(x => x.TimeZone).HasMaxLength(50);
        });

        builder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.StoreId);
            e.Property(c => c.Name).HasMaxLength(80);
            e.Property(c => c.NameAr).HasMaxLength(80);
            e.HasQueryFilter(c => !c.IsDeleted);
            e.HasOne(c => c.Store).WithMany(s => s.Categories)
                .HasForeignKey(c => c.StoreId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Product>(e =>
        {
            e.HasIndex(p => new { p.StoreId, p.IsDeleted });
            e.Property(p => p.Name).HasMaxLength(120);
            e.Property(p => p.NameAr).HasMaxLength(120);
            e.Property(p => p.Description).HasMaxLength(1000);
            e.Property(p => p.DescriptionAr).HasMaxLength(1000);
            // Optimistic concurrency on stock so simultaneous checkouts cannot oversell.
            e.Property(p => p.StockQuantity).IsConcurrencyToken();
            e.HasQueryFilter(p => !p.IsDeleted);
            e.HasOne(p => p.Store).WithMany(s => s.Products)
                .HasForeignKey(p => p.StoreId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Category).WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ProductImage>(e =>
        {
            e.Property(i => i.Path).HasMaxLength(300);
            e.HasOne(i => i.Product).WithMany(p => p.Images)
                .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductVariant>(e =>
        {
            e.Property(v => v.Name).HasMaxLength(60);
            e.Property(v => v.NameAr).HasMaxLength(60);
            e.HasOne(v => v.Product).WithMany(p => p.Variants)
                .HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductVariantOption>(e =>
        {
            e.Property(o => o.Name).HasMaxLength(60);
            e.Property(o => o.NameAr).HasMaxLength(60);
            e.HasOne(o => o.ProductVariant).WithMany(v => v.Options)
                .HasForeignKey(o => o.ProductVariantId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Customer>(e =>
        {
            e.HasIndex(c => new { c.StoreId, c.Phone }).IsUnique();
            e.Property(c => c.Name).HasMaxLength(100);
            e.Property(c => c.Phone).HasMaxLength(20);
            e.HasOne(c => c.Store).WithMany()
                .HasForeignKey(c => c.StoreId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Order>(e =>
        {
            e.HasIndex(o => new { o.StoreId, o.OrderNumber }).IsUnique();
            e.HasIndex(o => new { o.StoreId, o.Status });
            e.HasIndex(o => new { o.StoreId, o.CreatedAt });
            // Buyer account order history.
            e.HasIndex(o => new { o.BuyerUserId, o.CreatedAt });
            e.Property(o => o.OrderNumber).HasMaxLength(20);
            e.Property(o => o.CustomerName).HasMaxLength(100);
            e.Property(o => o.CustomerPhone).HasMaxLength(20);
            e.Property(o => o.DeliveryAddress).HasMaxLength(300);
            e.Property(o => o.GoogleMapsUrl).HasMaxLength(300);
            e.Property(o => o.PreferredTime).HasMaxLength(100);
            e.Property(o => o.Notes).HasMaxLength(500);
            e.HasOne(o => o.Store).WithMany(s => s.Orders)
                .HasForeignKey(o => o.StoreId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.Customer).WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.ProductName).HasMaxLength(120);
            e.Property(i => i.VariantsText).HasMaxLength(300);
            e.HasOne(i => i.Order).WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany()
                .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OrderStatusHistory>(e =>
        {
            e.HasIndex(h => new { h.OrderId, h.CreatedAt });
            e.Property(h => h.ChangedBy).HasMaxLength(20);
            e.HasOne(h => h.Order).WithMany(o => o.StatusHistory)
                .HasForeignKey(h => h.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatMessage>(e =>
        {
            e.HasIndex(m => new { m.OrderId, m.CreatedAt });
            // Store-wide unread badge: customer messages with ReadAt == null.
            e.HasIndex(m => new { m.StoreId, m.Sender, m.ReadAt });
            e.Property(m => m.Body).HasMaxLength(1000);
            e.HasOne(m => m.Order).WithMany()
                .HasForeignKey(m => m.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
            e.Property(n => n.Title).HasMaxLength(200);
            e.Property(n => n.Body).HasMaxLength(500);
        });

        builder.Entity<WhatsAppMessage>(e =>
        {
            e.HasIndex(m => m.WaMessageId);
            e.HasIndex(m => new { m.StoreId, m.CreatedAt });
            e.Property(m => m.Phone).HasMaxLength(20);
            e.Property(m => m.WaMessageId).HasMaxLength(128);
            e.Property(m => m.Error).HasMaxLength(500);
            e.HasOne(m => m.Store).WithMany()
                .HasForeignKey(m => m.StoreId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(m => m.Order).WithMany()
                .HasForeignKey(m => m.OrderId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.UserId);
            e.Property(t => t.TokenHash).HasMaxLength(88);
            e.Property(t => t.ReplacedByTokenHash).HasMaxLength(88);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Subscription>(e =>
        {
            e.HasIndex(s => s.StoreId).IsUnique();
            e.Property(s => s.ExternalCustomerId).HasMaxLength(100);
            e.Property(s => s.ExternalSubscriptionId).HasMaxLength(100);
        });

        builder.Entity<BuyerProfile>(e =>
        {
            e.HasIndex(p => p.UserId).IsUnique();
            e.Property(p => p.AvatarPath).HasMaxLength(300);
            e.Property(p => p.PreferredLanguage).HasMaxLength(5);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BuyerAddress>(e =>
        {
            e.HasIndex(a => a.UserId);
            e.Property(a => a.Label).HasMaxLength(40);
            e.Property(a => a.RecipientName).HasMaxLength(100);
            e.Property(a => a.Phone).HasMaxLength(20);
            e.Property(a => a.Governorate).HasMaxLength(50);
            e.Property(a => a.Wilayat).HasMaxLength(50);
            e.Property(a => a.City).HasMaxLength(80);
            e.Property(a => a.Area).HasMaxLength(100);
            e.Property(a => a.Street).HasMaxLength(100);
            e.Property(a => a.Building).HasMaxLength(50);
            e.Property(a => a.Apartment).HasMaxLength(50);
            e.Property(a => a.Notes).HasMaxLength(300);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FavoriteStore>(e =>
        {
            e.HasIndex(f => new { f.UserId, f.StoreId }).IsUnique();
            e.HasIndex(f => f.StoreId);
            // Match the Store soft-delete filter so favorites of deleted stores vanish too.
            e.HasQueryFilter(f => !f.Store.IsDeleted);
            e.HasOne(f => f.Store).WithMany()
                .HasForeignKey(f => f.StoreId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FavoriteProduct>(e =>
        {
            e.HasIndex(f => new { f.UserId, f.ProductId }).IsUnique();
            e.HasIndex(f => f.ProductId);
            e.HasQueryFilter(f => !f.Product.IsDeleted);
            e.HasOne(f => f.Product).WithMany()
                .HasForeignKey(f => f.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BuyerActivity>(e =>
        {
            // Recently-viewed / personalization reads.
            e.HasIndex(a => new { a.UserId, a.CreatedAt });
            // Popularity/trending aggregations.
            e.HasIndex(a => new { a.EventType, a.CreatedAt });
            e.HasIndex(a => a.StoreId);
            e.HasIndex(a => a.ProductId);
            e.Property(a => a.SearchQuery).HasMaxLength(120);
            // Scalar ids only — no FKs. Activity is fire-and-forget telemetry and must
            // never block or cascade from catalog deletes; readers join defensively.
        });

        builder.Entity<Offer>(e =>
        {
            e.HasIndex(o => new { o.StoreId, o.IsActive });
            e.HasIndex(o => o.EndsAt);
            e.HasQueryFilter(o => !o.Store.IsDeleted);
            e.Property(o => o.Title).HasMaxLength(100);
            e.Property(o => o.TitleAr).HasMaxLength(100);
            e.Property(o => o.Description).HasMaxLength(300);
            e.Property(o => o.DescriptionAr).HasMaxLength(300);
            e.HasOne(o => o.Store).WithMany()
                .HasForeignKey(o => o.StoreId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Review>(e =>
        {
            e.HasIndex(r => r.OrderId).IsUnique();
            e.HasIndex(r => new { r.StoreId, r.CreatedAt });
            e.HasIndex(r => r.UserId);
            e.Property(r => r.Comment).HasMaxLength(500);
            e.Property(r => r.ReviewerName).HasMaxLength(100);
            e.HasOne(r => r.Order).WithMany()
                .HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.UpdatedAt = utcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<StoreSettings>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.UpdatedAt = utcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
