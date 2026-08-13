using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;
using WhatsOrder.Infrastructure.Identity;

namespace WhatsOrder.Infrastructure.Persistence;

public static class DbSeeder
{
    public const string DemoEmail = "demo@whatsorder.om";
    public const string DemoBuyerEmail = "buyer@whatsorder.om";
    public const string DemoPassword = "Demo@1234";

    public static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in new[] { "Owner", "Buyer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }

    /// <summary>Idempotent demo data for development: three stores, a buyer, offers and a review.</summary>
    public static async Task SeedDemoDataAsync(
        AppDbContext db, UserManager<ApplicationUser> userManager, ILogger logger)
    {
        var owner = await EnsureUserAsync(userManager, DemoEmail, "Reem Al Balushi", ["Owner", "Buyer"], logger);
        if (owner is null)
            return;

        var alreem = await EnsureStoreAsync(db, owner.Id, "alreem", store =>
        {
            store.Name = "Al Reem Cakes";
            store.NameAr = "حلويات الريم";
            store.Description = "Handmade cakes and sweets, baked fresh every day in Muscat.";
            store.DescriptionAr = "كيك وحلويات منزلية طازجة يومياً في مسقط.";
            store.WhatsAppNumber = "+96890000001";
            store.InstagramHandle = "alreem.cakes";
            store.LocationText = "Al Khuwair, Muscat";
            store.Governorate = "Muscat";
            store.Wilayat = "Bawshar";
            store.Settings.DeliveryFee = 1.500m;
            store.Settings.MinimumOrderAmount = 3.000m;
            store.Subscription.Plan = SubscriptionPlan.Pro;

            var cakes = new Category { StoreId = store.Id, Name = "Cakes", NameAr = "كيك", SortOrder = 0 };
            var cookies = new Category { StoreId = store.Id, Name = "Cookies", NameAr = "كوكيز", SortOrder = 1 };
            var drinks = new Category { StoreId = store.Id, Name = "Drinks", NameAr = "مشروبات", SortOrder = 2 };
            db.Categories.AddRange(cakes, cookies, drinks);

            var celebrationCake = new Product
            {
                StoreId = store.Id,
                CategoryId = cakes.Id,
                Name = "Celebration Cake",
                NameAr = "كيكة الاحتفال",
                Description = "Moist layered cake, made to order. Choose your size and flavor.",
                DescriptionAr = "كيكة طبقات طرية تحضر حسب الطلب. اختر الحجم والنكهة.",
                Price = 8.500m,
                IsFeatured = true
            };
            var size = new ProductVariant { ProductId = celebrationCake.Id, Name = "Size", NameAr = "الحجم", IsRequired = true, SortOrder = 0 };
            size.Options.Add(new ProductVariantOption { ProductVariantId = size.Id, Name = "Small", NameAr = "صغير", PriceAdjustment = 0, SortOrder = 0 });
            size.Options.Add(new ProductVariantOption { ProductVariantId = size.Id, Name = "Medium", NameAr = "وسط", PriceAdjustment = 2.000m, SortOrder = 1 });
            size.Options.Add(new ProductVariantOption { ProductVariantId = size.Id, Name = "Large", NameAr = "كبير", PriceAdjustment = 4.500m, SortOrder = 2 });
            var flavor = new ProductVariant { ProductId = celebrationCake.Id, Name = "Flavor", NameAr = "النكهة", IsRequired = true, SortOrder = 1 };
            flavor.Options.Add(new ProductVariantOption { ProductVariantId = flavor.Id, Name = "Chocolate", NameAr = "شوكولاتة", PriceAdjustment = 0, SortOrder = 0 });
            flavor.Options.Add(new ProductVariantOption { ProductVariantId = flavor.Id, Name = "Pistachio", NameAr = "فستق", PriceAdjustment = 1.000m, SortOrder = 1 });
            flavor.Options.Add(new ProductVariantOption { ProductVariantId = flavor.Id, Name = "Vanilla", NameAr = "فانيليا", PriceAdjustment = 0, SortOrder = 2 });
            celebrationCake.Variants.Add(size);
            celebrationCake.Variants.Add(flavor);

            db.Products.AddRange(
                celebrationCake,
                new Product
                {
                    StoreId = store.Id, CategoryId = cookies.Id,
                    Name = "Cookies Box", NameAr = "علبة كوكيز",
                    Description = "12 assorted butter cookies.", DescriptionAr = "12 قطعة كوكيز بالزبدة مشكلة.",
                    Price = 3.000m, DiscountedPrice = 2.500m, StockQuantity = 25, IsFeatured = true
                },
                new Product
                {
                    StoreId = store.Id, CategoryId = drinks.Id,
                    Name = "Saffron Latte", NameAr = "لاتيه بالزعفران",
                    Description = "Chilled saffron latte with a hint of cardamom.", DescriptionAr = "لاتيه بارد بالزعفران مع لمسة هيل.",
                    Price = 1.800m, StockQuantity = 40
                },
                new Product
                {
                    StoreId = store.Id, CategoryId = cakes.Id,
                    Name = "Omani Date Cake", NameAr = "كيكة التمر العمانية",
                    Description = "Rich date cake with local dates and caramel drizzle.", DescriptionAr = "كيكة تمر غنية بالتمور المحلية وصوص الكراميل.",
                    Price = 5.500m
                });

            db.Offers.Add(new Offer
            {
                StoreId = store.Id,
                Title = "10% off your first week orders",
                TitleAr = "خصم ١٠٪ على طلبات الأسبوع الأول",
                Description = "Automatic 10% discount on orders of 5 OMR or more.",
                DescriptionAr = "خصم تلقائي ١٠٪ على الطلبات بقيمة ٥ ريال أو أكثر.",
                Type = OfferType.Percentage,
                DiscountValue = 10m,
                MinimumOrderAmount = 5.000m,
                StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = DateTime.UtcNow.AddDays(30)
            });
        });

        var spiceOwner = await EnsureUserAsync(userManager, "spice@whatsorder.om", "Salim Al Habsi", ["Owner", "Buyer"], logger);
        if (spiceOwner is not null)
        {
            await EnsureStoreAsync(db, spiceOwner.Id, "spice-souq", store =>
            {
                store.Name = "Spice Souq Kitchen";
                store.NameAr = "مطبخ سوق التوابل";
                store.Description = "Traditional Omani home cooking — shuwa, majboos and fresh bread.";
                store.DescriptionAr = "أكل عماني بيتي تقليدي — شواء ومجبوس وخبز طازج.";
                store.WhatsAppNumber = "+96890000002";
                store.LocationText = "Muttrah, Muscat";
                store.Governorate = "Muscat";
                store.Wilayat = "Muttrah";
                store.Settings.DeliveryFee = 1.000m;
                store.Settings.MinimumOrderAmount = 2.000m;

                var mains = new Category { StoreId = store.Id, Name = "Mains", NameAr = "أطباق رئيسية", SortOrder = 0 };
                var bread = new Category { StoreId = store.Id, Name = "Bread", NameAr = "خبز", SortOrder = 1 };
                db.Categories.AddRange(mains, bread);

                db.Products.AddRange(
                    new Product
                    {
                        StoreId = store.Id, CategoryId = mains.Id,
                        Name = "Chicken Majboos", NameAr = "مجبوس دجاج",
                        Description = "Fragrant spiced rice with slow-cooked chicken.", DescriptionAr = "أرز متبل مع دجاج مطبوخ ببطء.",
                        Price = 2.500m, IsFeatured = true
                    },
                    new Product
                    {
                        StoreId = store.Id, CategoryId = mains.Id,
                        Name = "Lamb Shuwa Plate", NameAr = "طبق شواء لحم",
                        Description = "Weekend special — banana-leaf marinated lamb.", DescriptionAr = "طبق نهاية الأسبوع — لحم متبل بورق الموز.",
                        Price = 4.500m, IsFeatured = true
                    },
                    new Product
                    {
                        StoreId = store.Id, CategoryId = bread.Id,
                        Name = "Omani Bread (5 pcs)", NameAr = "خبز عماني (٥ قطع)",
                        Description = "Paper-thin crispy bread, baked to order.", DescriptionAr = "خبز رقيق مقرمش يخبز عند الطلب.",
                        Price = 0.800m, StockQuantity = 60
                    });

                db.Offers.Add(new Offer
                {
                    StoreId = store.Id,
                    Title = "Free delivery this month",
                    TitleAr = "توصيل مجاني هذا الشهر",
                    Description = "No delivery fee on any order.",
                    DescriptionAr = "بدون رسوم توصيل على أي طلب.",
                    Type = OfferType.FreeDelivery,
                    MinimumOrderAmount = 0m,
                    StartsAt = DateTime.UtcNow.AddDays(-1),
                    EndsAt = DateTime.UtcNow.AddDays(28)
                });
            });
        }

        var roasterOwner = await EnsureUserAsync(userManager, "roasters@whatsorder.om", "Maha Al Zadjali", ["Owner", "Buyer"], logger);
        if (roasterOwner is not null)
        {
            await EnsureStoreAsync(db, roasterOwner.Id, "muscat-roasters", store =>
            {
                store.Name = "Muscat Roasters";
                store.NameAr = "محمصة مسقط";
                store.Description = "Specialty coffee beans roasted weekly, plus brewing gear.";
                store.DescriptionAr = "بن مختص يحمص أسبوعياً مع أدوات التحضير.";
                store.WhatsAppNumber = "+96890000003";
                store.LocationText = "Al Mouj, Muscat";
                store.Governorate = "Muscat";
                store.Wilayat = "Al Seeb";
                store.Settings.DeliveryFee = 2.000m;
                store.Settings.MinimumOrderAmount = 0m;

                var beans = new Category { StoreId = store.Id, Name = "Coffee Beans", NameAr = "حبوب قهوة", SortOrder = 0 };
                db.Categories.Add(beans);

                db.Products.AddRange(
                    new Product
                    {
                        StoreId = store.Id, CategoryId = beans.Id,
                        Name = "Ethiopia Yirgacheffe 250g", NameAr = "إثيوبيا يرغاتشيف ٢٥٠ غ",
                        Description = "Floral and citrus, light roast.", DescriptionAr = "زهري وحمضي، تحميص فاتح.",
                        Price = 4.000m, StockQuantity = 30, IsFeatured = true
                    },
                    new Product
                    {
                        StoreId = store.Id, CategoryId = beans.Id,
                        Name = "Colombia Supremo 250g", NameAr = "كولومبيا سوبريمو ٢٥٠ غ",
                        Description = "Chocolate and caramel, medium roast.", DescriptionAr = "شوكولاتة وكراميل، تحميص وسط.",
                        Price = 3.500m, DiscountedPrice = 3.000m, StockQuantity = 25
                    });
            });
        }

        // A demo buyer with one completed, reviewed order at Al Reem.
        var buyer = await EnsureUserAsync(userManager, DemoBuyerEmail, "Ahmed Al Farsi", ["Buyer"], logger);
        if (buyer is not null && alreem is not null && !await db.Orders.AnyAsync(o => o.BuyerUserId == buyer.Id))
        {
            var product = await db.Products
                .FirstOrDefaultAsync(p => p.StoreId == alreem.Id && p.Name == "Cookies Box");
            if (product is not null)
            {
                alreem.OrderSequence++;
                var order = new Order
                {
                    StoreId = alreem.Id,
                    OrderNumber = $"WO-{alreem.OrderSequence}",
                    BuyerUserId = buyer.Id,
                    CustomerName = buyer.DisplayName,
                    CustomerPhone = "+96890000010",
                    Status = OrderStatus.Completed,
                    FulfillmentMethod = FulfillmentMethod.Pickup,
                    Subtotal = 2.500m,
                    DeliveryFee = 0m,
                    Discount = 0.500m,
                    Total = 2.500m
                };
                order.Items.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = 2.500m,
                    Quantity = 1,
                    LineTotal = 2.500m
                });
                order.StatusHistory.Add(new OrderStatusHistory
                {
                    OrderId = order.Id, PreviousStatus = null, NewStatus = OrderStatus.Completed, ChangedBy = "system"
                });
                db.Orders.Add(order);
                db.Reviews.Add(new Review
                {
                    OrderId = order.Id,
                    StoreId = alreem.Id,
                    UserId = buyer.Id,
                    Rating = 5,
                    Comment = "The cookies were amazing — fresh and beautifully packed!",
                    ReviewerName = buyer.DisplayName
                });
                await db.SaveChangesAsync();
            }
        }

        logger.LogInformation("Demo data ready: stores /alreem, /spice-souq, /muscat-roasters; owner {Owner} and buyer {Buyer} (password {Password})",
            DemoEmail, DemoBuyerEmail, DemoPassword);
    }

    /// <summary>Creates the user if missing and guarantees the given roles either way.</summary>
    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string displayName,
        string[] roles, ILogger logger)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName
            };
            var created = await userManager.CreateAsync(user, DemoPassword);
            if (!created.Succeeded)
            {
                logger.LogWarning("Could not create demo user {Email}: {Errors}",
                    email, string.Join(", ", created.Errors.Select(e => e.Description)));
                return null;
            }
        }

        var current = await userManager.GetRolesAsync(user);
        var missing = roles.Except(current).ToArray();
        if (missing.Length > 0)
            await userManager.AddToRolesAsync(user, missing);
        return user;
    }

    /// <summary>Creates the store (with default settings + free subscription) if the slug is absent.</summary>
    private static async Task<Store?> EnsureStoreAsync(
        AppDbContext db, Guid ownerId, string slug, Action<Store> configure)
    {
        var existing = await db.Stores.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Slug == slug);
        if (existing is not null)
            return existing;

        var store = new Store { OwnerId = ownerId, Slug = slug, IsAcceptingOrders = true };
        store.Settings = new StoreSettings
        {
            StoreId = store.Id,
            OpeningHoursJson = OpeningHours.Serialize(OpeningHours.Defaults()),
            DefaultLanguage = "en"
        };
        store.Subscription = new Subscription
        {
            StoreId = store.Id,
            Plan = SubscriptionPlan.Free,
            StartsAt = DateTime.UtcNow
        };
        configure(store);
        db.Stores.Add(store);
        await db.SaveChangesAsync();
        return store;
    }
}
