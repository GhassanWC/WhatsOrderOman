using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Reviews;

public class ReviewService(IAppDbContext db, IStoreContext storeContext)
{
    /// <summary>Buyers may review only their own completed orders, once per order.</summary>
    public async Task<ReviewDto> CreateAsync(
        Guid userId, string reviewerName, Guid orderId, CreateReviewRequest request,
        CancellationToken ct = default)
    {
        var order = await db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == userId, ct)
            ?? throw new NotFoundException("Order not found.");

        if (order.Status != OrderStatus.Completed)
            throw new BusinessRuleException("review_not_allowed",
                "Only completed orders can be reviewed.");

        if (await db.Reviews.AnyAsync(r => r.OrderId == order.Id, ct))
            throw new BusinessRuleException("already_reviewed",
                "You have already reviewed this order.");

        var review = new Review
        {
            OrderId = order.Id,
            StoreId = order.StoreId,
            UserId = userId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            ReviewerName = string.IsNullOrWhiteSpace(reviewerName) ? "Customer" : reviewerName.Trim()
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);

        return review.ToDto();
    }

    /// <summary>Public store reviews, newest first.</summary>
    public async Task<StoreReviewsPage> GetForStoreAsync(
        string slug, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        Guid storeId = await db.Stores
            .Where(s => s.Slug == Slugs.Normalize(slug))
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Store not found.");

        return await PageAsync(storeId, page, pageSize, ct);
    }

    /// <summary>The authenticated owner's store reviews.</summary>
    public async Task<StoreReviewsPage> GetForMyStoreAsync(
        int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        return await PageAsync(store.Id, page, pageSize, ct);
    }

    private async Task<StoreReviewsPage> PageAsync(Guid storeId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.Reviews.Where(r => r.StoreId == storeId);
        var total = await query.CountAsync(ct);
        var average = total == 0 ? (double?)null : await query.AverageAsync(r => (double)r.Rating, ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new StoreReviewsPage(
            items.Select(r => r.ToDto()).ToList(),
            total, page, pageSize,
            average is null ? null : Math.Round(average.Value, 1), total);
    }
}

public static class ReviewMapping
{
    public static ReviewDto ToDto(this Review review) => new(
        review.Id, review.OrderId, review.Rating, review.Comment,
        review.ReviewerName, review.CreatedAt);
}

/// <summary>Aggregated store ratings for card listings — one grouped query per page.</summary>
public static class ReviewQueries
{
    public sealed record StoreRating(double Average, int Count);

    public static async Task<Dictionary<Guid, StoreRating>> ForStoresAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> storeIds, CancellationToken ct)
    {
        if (storeIds.Count == 0)
            return [];

        var rows = await db.Reviews
            .Where(r => storeIds.Contains(r.StoreId))
            .GroupBy(r => r.StoreId)
            .Select(g => new { g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Key,
            r => new StoreRating(Math.Round(r.Average, 1), r.Count));
    }
}
