using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Orders;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Dashboard;

public sealed record TopProductDto(string Name, int Quantity, decimal Revenue);

public sealed record DashboardSummaryDto(
    int OrdersToday,
    decimal RevenueToday,
    int OrdersThisMonth,
    decimal RevenueThisMonth,
    decimal AverageOrderValue,
    int NewOrdersCount,
    int ProductsCount,
    List<TopProductDto> TopProducts,
    List<OrderListItemDto> RecentOrders);

public class DashboardService(IAppDbContext db, IStoreContext storeContext)
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var utcNow = DateTime.UtcNow;
        var dayStart = OrderService.OmanDayStartUtc(utcNow);
        var monthStart = OrderService.OmanMonthStartUtc(utcNow);

        // Cancelled orders count for volume but never for revenue.
        // Totals are summed in memory: decimal aggregates keep provider portability
        // (SQLite in tests) and these result sets are small by definition.
        var monthOrders = await db.Orders
            .Where(o => o.StoreId == store.Id && o.CreatedAt >= monthStart)
            .Select(o => new { o.CreatedAt, o.Status, o.Total })
            .ToListAsync(ct);

        var todayOrders = monthOrders.Where(o => o.CreatedAt >= dayStart).ToList();
        var monthRevenueOrders = monthOrders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        var todayRevenueOrders = todayOrders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        var newOrdersCount = await db.Orders
            .CountAsync(o => o.StoreId == store.Id && o.Status == OrderStatus.New, ct);
        var productsCount = await db.Products.CountAsync(p => p.StoreId == store.Id, ct);

        var monthItems = await db.OrderItems
            .Where(i => i.Order.StoreId == store.Id
                        && i.Order.CreatedAt >= monthStart
                        && i.Order.Status != OrderStatus.Cancelled)
            .Select(i => new { i.ProductName, i.Quantity, i.LineTotal })
            .ToListAsync(ct);

        var topProducts = monthItems
            .GroupBy(i => i.ProductName)
            .Select(g => new TopProductDto(g.Key, g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal)))
            .OrderByDescending(t => t.Quantity)
            .Take(5)
            .ToList();

        var recentOrders = (await db.Orders
                .Where(o => o.StoreId == store.Id)
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync(ct))
            .Select(o => o.ToListItemDto())
            .ToList();

        var monthRevenue = monthRevenueOrders.Sum(o => o.Total);

        return new DashboardSummaryDto(
            OrdersToday: todayOrders.Count,
            RevenueToday: todayRevenueOrders.Sum(o => o.Total),
            OrdersThisMonth: monthOrders.Count,
            RevenueThisMonth: monthRevenue,
            AverageOrderValue: monthRevenueOrders.Count > 0
                ? Money.Round(monthRevenue / monthRevenueOrders.Count)
                : 0,
            NewOrdersCount: newOrdersCount,
            ProductsCount: productsCount,
            TopProducts: topProducts,
            RecentOrders: recentOrders);
    }
}
