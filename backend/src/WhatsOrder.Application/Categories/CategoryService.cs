using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Categories;

public class CategoryService(IAppDbContext db, IStoreContext storeContext)
{
    public async Task<List<CategoryDto>> ListAsync(CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);

        return await db.Categories
            .Where(c => c.StoreId == store.Id)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id, c.Name, c.NameAr, c.SortOrder, c.IsActive,
                c.Products.Count(p => !p.IsDeleted)))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);

        var category = new Category
        {
            StoreId = store.Id,
            Name = request.Name.Trim(),
            NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.NameAr, category.SortOrder, category.IsActive, 0);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.StoreId == store.Id, ct)
            ?? throw new NotFoundException("Category not found.");

        category.Name = request.Name.Trim();
        category.NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim();
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        var productsCount = await db.Products.CountAsync(p => p.CategoryId == id, ct);
        return new CategoryDto(category.Id, category.Name, category.NameAr, category.SortOrder, category.IsActive, productsCount);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.StoreId == store.Id, ct)
            ?? throw new NotFoundException("Category not found.");

        // Products keep existing but become uncategorized.
        var products = await db.Products.Where(p => p.CategoryId == id).ToListAsync(ct);
        foreach (var product in products)
            product.CategoryId = null;

        category.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }
}
