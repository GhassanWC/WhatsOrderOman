using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Products;

public sealed record VariantOptionDto(
    Guid? Id, string Name, string? NameAr, decimal PriceAdjustment, bool IsAvailable, int SortOrder);

public sealed record VariantDto(
    Guid? Id, string Name, string? NameAr, bool IsRequired, int SortOrder, List<VariantOptionDto> Options);

public sealed record ProductImageDto(Guid Id, string Url, int SortOrder, bool IsPrimary);

public sealed record ProductDto(
    Guid Id,
    Guid? CategoryId,
    string? CategoryName,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    decimal Price,
    decimal? DiscountedPrice,
    int? StockQuantity,
    bool IsAvailable,
    bool IsFeatured,
    List<ProductImageDto> Images,
    List<VariantDto> Variants,
    DateTime CreatedAt);

public sealed record SaveProductRequest(
    Guid? CategoryId,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    decimal Price,
    decimal? DiscountedPrice,
    int? StockQuantity,
    bool IsAvailable,
    bool IsFeatured,
    List<VariantDto>? Variants);

public sealed record SetAvailabilityRequest(bool IsAvailable);

public static class ProductMapping
{
    public static ProductDto ToDto(this Product p) => new(
        p.Id,
        p.CategoryId,
        p.Category?.Name,
        p.Name,
        p.NameAr,
        p.Description,
        p.DescriptionAr,
        p.Price,
        p.DiscountedPrice,
        p.StockQuantity,
        p.IsAvailable,
        p.IsFeatured,
        p.Images.OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, i.Path, i.SortOrder, i.IsPrimary)).ToList(),
        p.Variants.OrderBy(v => v.SortOrder)
            .Select(v => new VariantDto(
                v.Id, v.Name, v.NameAr, v.IsRequired, v.SortOrder,
                v.Options.OrderBy(o => o.SortOrder)
                    .Select(o => new VariantOptionDto(o.Id, o.Name, o.NameAr, o.PriceAdjustment, o.IsAvailable, o.SortOrder))
                    .ToList()))
            .ToList(),
        p.CreatedAt);
}
