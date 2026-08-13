using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Activity;

/// <summary>
/// Records shopping signals (views, searches, favorites, carts, orders) that power
/// recently-viewed lists and rule-based recommendations. Best-effort by design:
/// tracking must never fail or slow down the request that triggered it.
/// </summary>
public class ActivityTracker(IAppDbContext db, ILogger<ActivityTracker> logger)
{
    /// <summary>Repeat views of the same thing inside this window are not re-recorded.</summary>
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(30);

    public async Task TrackAsync(
        BuyerEventType eventType,
        Guid? userId,
        Guid? storeId = null,
        Guid? productId = null,
        Guid? categoryId = null,
        string? searchQuery = null,
        CancellationToken ct = default)
    {
        try
        {
            // Anonymous visitors contribute only to aggregate signals (trending/popular).
            if (userId is null && eventType is not (BuyerEventType.StoreViewed
                or BuyerEventType.ProductViewed or BuyerEventType.Searched))
                return;

            if (eventType is BuyerEventType.StoreViewed or BuyerEventType.ProductViewed && userId is not null)
            {
                var since = DateTime.UtcNow - DedupeWindow;
                var alreadySeen = await db.BuyerActivities.AnyAsync(a =>
                    a.UserId == userId && a.EventType == eventType &&
                    a.StoreId == storeId && a.ProductId == productId &&
                    a.CreatedAt >= since, ct);
                if (alreadySeen)
                    return;
            }

            var query = searchQuery?.Trim();
            if (query is { Length: > 120 })
                query = query[..120];

            db.BuyerActivities.Add(new BuyerActivity
            {
                UserId = userId,
                EventType = eventType,
                StoreId = storeId,
                ProductId = productId,
                CategoryId = categoryId,
                SearchQuery = string.IsNullOrWhiteSpace(query) ? null : query
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to track {EventType} activity", eventType);
        }
    }
}
