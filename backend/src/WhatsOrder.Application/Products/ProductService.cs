using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Products;

public class ProductService(IAppDbContext db, IStoreContext storeContext, IFileStorage files)
{
    private const int MaxImagesPerProduct = 5;

    public async Task<PagedResult<ProductDto>> ListAsync(
        string? search, Guid? categoryId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Products
            .Where(p => p.StoreId == store.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                (p.NameAr != null && p.NameAr.Contains(term)));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId);

        var total = await query.CountAsync(ct);
        var products = await query
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants).ThenInclude(v => v.Options)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(products.Select(p => p.ToDto()).ToList(), total, page, pageSize);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var product = await LoadOwnedProductAsync(id, ct);
        return product.ToDto();
    }

    public async Task<ProductDto> CreateAsync(SaveProductRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);

        var limits = PlanCatalog.For(store.Subscription.Plan);
        if (limits.MaxProducts is { } maxProducts)
        {
            var count = await db.Products.CountAsync(p => p.StoreId == store.Id, ct);
            if (count >= maxProducts)
                throw new BusinessRuleException("plan_limit_products",
                    $"The {store.Subscription.Plan} plan allows up to {maxProducts} products. Upgrade to Pro for unlimited products.");
        }

        await EnsureCategoryBelongsToStoreAsync(request.CategoryId, store.Id, ct);

        var product = new Product
        {
            StoreId = store.Id,
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            NameAr = Clean(request.NameAr),
            Description = Clean(request.Description),
            DescriptionAr = Clean(request.DescriptionAr),
            Price = Money.Round(request.Price),
            DiscountedPrice = request.DiscountedPrice.HasValue ? Money.Round(request.DiscountedPrice.Value) : null,
            StockQuantity = request.StockQuantity,
            IsAvailable = request.IsAvailable,
            IsFeatured = request.IsFeatured
        };

        ApplyVariants(product, request.Variants ?? []);

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        product.Category = request.CategoryId.HasValue
            ? await db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct)
            : null;
        return product.ToDto();
    }

    public async Task<ProductDto> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var product = await LoadOwnedProductAsync(id, ct);

        await EnsureCategoryBelongsToStoreAsync(request.CategoryId, store.Id, ct);

        product.CategoryId = request.CategoryId;
        product.Name = request.Name.Trim();
        product.NameAr = Clean(request.NameAr);
        product.Description = Clean(request.Description);
        product.DescriptionAr = Clean(request.DescriptionAr);
        product.Price = Money.Round(request.Price);
        product.DiscountedPrice = request.DiscountedPrice.HasValue ? Money.Round(request.DiscountedPrice.Value) : null;
        product.StockQuantity = request.StockQuantity;
        product.IsAvailable = request.IsAvailable;
        product.IsFeatured = request.IsFeatured;

        SyncVariants(product, request.Variants ?? []);

        await db.SaveChangesAsync(ct);

        product.Category = request.CategoryId.HasValue
            ? await db.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct)
            : null;
        return product.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await LoadOwnedProductAsync(id, ct);
        product.IsDeleted = true;
        product.IsAvailable = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProductDto> SetAvailabilityAsync(Guid id, bool isAvailable, CancellationToken ct = default)
    {
        var product = await LoadOwnedProductAsync(id, ct);
        product.IsAvailable = isAvailable;
        await db.SaveChangesAsync(ct);
        return product.ToDto();
    }

    public async Task<ProductDto> AddImageAsync(Guid id, Stream content, string extension, CancellationToken ct = default)
    {
        var product = await LoadOwnedProductAsync(id, ct);

        if (product.Images.Count >= MaxImagesPerProduct)
            throw new BusinessRuleException("too_many_images",
                $"A product can have at most {MaxImagesPerProduct} images.");

        var path = await files.SaveAsync(content, extension, "products", ct);
        var image = new ProductImage
        {
            ProductId = product.Id,
            Path = path,
            SortOrder = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1,
            IsPrimary = product.Images.Count == 0
        };
        product.Images.Add(image);
        // Explicit Add: BaseEntity pre-generates Guid keys, so an entity merely discovered
        // via the tracked product's navigation would be treated as Modified, not Added.
        db.ProductImages.Add(image);
        await db.SaveChangesAsync(ct);
        return product.ToDto();
    }

    public async Task<ProductDto> DeleteImageAsync(Guid id, Guid imageId, CancellationToken ct = default)
    {
        var product = await LoadOwnedProductAsync(id, ct);
        var image = product.Images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new NotFoundException("Image not found.");

        product.Images.Remove(image);
        db.ProductImages.Remove(image);
        if (image.IsPrimary && product.Images.Count > 0)
            product.Images.OrderBy(i => i.SortOrder).First().IsPrimary = true;

        await db.SaveChangesAsync(ct);
        await files.DeleteAsync(image.Path, ct);
        return product.ToDto();
    }

    /// <summary>Reorders images to match the given id list; ids must be exactly this product's images.</summary>
    public async Task<ProductDto> ReorderImagesAsync(Guid id, List<Guid> imageIds, CancellationToken ct = default)
    {
        var product = await LoadOwnedProductAsync(id, ct);

        var current = product.Images.Select(i => i.Id).ToHashSet();
        if (imageIds.Count != current.Count || !imageIds.All(current.Contains))
            throw new BusinessRuleException("invalid_image_order",
                "The image list does not match this product's images.");

        foreach (var (imageId, index) in imageIds.Select((imageId, index) => (imageId, index)))
            product.Images.First(i => i.Id == imageId).SortOrder = index;

        await db.SaveChangesAsync(ct);
        return product.ToDto();
    }

    public async Task<ProductDto> SetPrimaryImageAsync(Guid id, Guid imageId, CancellationToken ct = default)
    {
        var product = await LoadOwnedProductAsync(id, ct);
        var image = product.Images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new NotFoundException("Image not found.");

        foreach (var other in product.Images)
            other.IsPrimary = other.Id == imageId;

        await db.SaveChangesAsync(ct);
        return product.ToDto();
    }

    private async Task<Product> LoadOwnedProductAsync(Guid id, CancellationToken ct)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        return await db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants).ThenInclude(v => v.Options)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == store.Id, ct)
            ?? throw new NotFoundException("Product not found.");
    }

    private async Task EnsureCategoryBelongsToStoreAsync(Guid? categoryId, Guid storeId, CancellationToken ct)
    {
        if (!categoryId.HasValue)
            return;
        var ok = await db.Categories.AnyAsync(c => c.Id == categoryId && c.StoreId == storeId, ct);
        if (!ok)
            throw new BusinessRuleException("invalid_category", "Category not found.");
    }

    private static void ApplyVariants(Product product, List<VariantDto> variants)
    {
        foreach (var (variant, vIndex) in variants.Select((v, i) => (v, i)))
        {
            var entity = new ProductVariant
            {
                ProductId = product.Id,
                Name = variant.Name.Trim(),
                NameAr = Clean(variant.NameAr),
                IsRequired = variant.IsRequired,
                SortOrder = vIndex
            };
            foreach (var (option, oIndex) in variant.Options.Select((o, i) => (o, i)))
            {
                entity.Options.Add(new ProductVariantOption
                {
                    ProductVariantId = entity.Id,
                    Name = option.Name.Trim(),
                    NameAr = Clean(option.NameAr),
                    PriceAdjustment = Money.Round(option.PriceAdjustment),
                    IsAvailable = option.IsAvailable,
                    SortOrder = oIndex
                });
            }
            product.Variants.Add(entity);
        }
    }

    /// <summary>Match by id: update existing, add new, remove missing (and same for options).</summary>
    private void SyncVariants(Product product, List<VariantDto> variants)
    {
        var keptVariantIds = variants.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();
        foreach (var removed in product.Variants.Where(v => !keptVariantIds.Contains(v.Id)).ToList())
        {
            product.Variants.Remove(removed);
            db.ProductVariants.Remove(removed);
        }

        foreach (var (variantDto, vIndex) in variants.Select((v, i) => (v, i)))
        {
            var variant = variantDto.Id.HasValue
                ? product.Variants.FirstOrDefault(v => v.Id == variantDto.Id.Value)
                : null;

            if (variant is null)
            {
                variant = new ProductVariant { ProductId = product.Id };
                product.Variants.Add(variant);
                // Explicit Add — pre-generated Guid keys make nav-discovered entities Modified.
                db.ProductVariants.Add(variant);
            }

            variant.Name = variantDto.Name.Trim();
            variant.NameAr = Clean(variantDto.NameAr);
            variant.IsRequired = variantDto.IsRequired;
            variant.SortOrder = vIndex;

            var keptOptionIds = variantDto.Options.Where(o => o.Id.HasValue).Select(o => o.Id!.Value).ToHashSet();
            foreach (var removed in variant.Options.Where(o => !keptOptionIds.Contains(o.Id)).ToList())
            {
                variant.Options.Remove(removed);
                db.ProductVariantOptions.Remove(removed);
            }

            foreach (var (optionDto, oIndex) in variantDto.Options.Select((o, i) => (o, i)))
            {
                var option = optionDto.Id.HasValue
                    ? variant.Options.FirstOrDefault(o => o.Id == optionDto.Id.Value)
                    : null;

                if (option is null)
                {
                    option = new ProductVariantOption { ProductVariantId = variant.Id };
                    variant.Options.Add(option);
                    db.ProductVariantOptions.Add(option); // same pre-generated-key reason
                }

                option.Name = optionDto.Name.Trim();
                option.NameAr = Clean(optionDto.NameAr);
                option.PriceAdjustment = Money.Round(optionDto.PriceAdjustment);
                option.IsAvailable = optionDto.IsAvailable;
                option.SortOrder = oIndex;
            }
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
