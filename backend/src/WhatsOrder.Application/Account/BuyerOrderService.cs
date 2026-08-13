using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Orders;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Application.Reviews;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Account;

/// <summary>
/// The buyer's own orders and conversations. Ownership is always Order.BuyerUserId ==
/// the authenticated user — client-supplied ids are only ever lookups within that scope.
/// </summary>
public class BuyerOrderService(IAppDbContext db, ICurrentUser currentUser)
{
    private static readonly OrderStatus[] TerminalStatuses =
        [OrderStatus.Completed, OrderStatus.Cancelled, OrderStatus.Rejected];

    public async Task<BuyerOrdersPage> ListAsync(
        string? filter, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var userId = RequireUser();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = db.Orders.Where(o => o.BuyerUserId == userId);
        query = (filter ?? "all").ToLowerInvariant() switch
        {
            "active" => query.Where(o => !TerminalStatuses.Contains(o.Status)),
            "completed" => query.Where(o => o.Status == OrderStatus.Completed),
            "cancelled" => query.Where(o => o.Status == OrderStatus.Cancelled
                                            || o.Status == OrderStatus.Rejected),
            _ => query
        };

        var total = await query.CountAsync(ct);
        var activeCount = await db.Orders.CountAsync(
            o => o.BuyerUserId == userId && !TerminalStatuses.Contains(o.Status), ct);

        var orders = await query
            .Include(o => o.Items)
            .Include(o => o.Store)
            .AsSplitQuery()
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var orderIds = orders.Select(o => o.Id).ToList();
        var unread = await UnreadByOrderAsync(orderIds, ct);
        var reviewed = await db.Reviews
            .Where(r => orderIds.Contains(r.OrderId))
            .Select(r => r.OrderId)
            .ToListAsync(ct);

        var items = orders.Select(o =>
        {
            var summary = string.Join(", ", o.Items.Select(i => $"{i.Quantity}× {i.ProductName}"));
            if (summary.Length > 90)
                summary = summary[..87] + "…";

            return new BuyerOrderListItemDto(
                o.Id, o.OrderNumber, o.Status, o.FulfillmentMethod, o.Total,
                o.Items.Sum(i => i.Quantity), summary,
                unread.GetValueOrDefault(o.Id),
                o.Store.Slug, o.Store.Name, o.Store.NameAr, o.Store.LogoPath,
                CanReview: o.Status == OrderStatus.Completed && !reviewed.Contains(o.Id),
                o.CreatedAt);
        }).ToList();

        return new BuyerOrdersPage(items, total, page, pageSize, activeCount);
    }

    public async Task<BuyerOrderDto> GetAsync(Guid orderId, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .Include(o => o.Store)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == userId, ct)
            ?? throw new NotFoundException("Order not found.");

        var unread = await UnreadByOrderAsync([order.Id], ct);
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.OrderId == order.Id, ct);

        return new BuyerOrderDto(
            order.Id, order.OrderNumber, order.Status, order.FulfillmentMethod,
            order.DeliveryAddress, order.PreferredTime, order.Notes,
            order.Subtotal, order.DeliveryFee, order.Discount, order.Total,
            order.Items.Select(i => i.ToDto()).ToList(),
            order.EstimatedReadyAt,
            order.StatusHistory.OrderBy(h => h.CreatedAt).Select(h => h.ToDto()).ToList(),
            unread.GetValueOrDefault(order.Id),
            order.Store.Slug, order.Store.Name, order.Store.NameAr, order.Store.LogoPath,
            order.Store.WhatsAppNumber,
            CanReview: order.Status == OrderStatus.Completed && review is null,
            review?.ToDto(),
            order.CreatedAt);
    }

    /// <summary>Messaging inbox: the buyer's orders that have at least one chat message.</summary>
    public async Task<List<BuyerConversationDto>> GetConversationsAsync(
        string? search, CancellationToken ct = default)
    {
        var userId = RequireUser();

        var conversations = await db.ChatMessages
            .Where(m => db.Orders.Any(o => o.Id == m.OrderId && o.BuyerUserId == userId))
            .GroupBy(m => m.OrderId)
            .Select(g => new
            {
                OrderId = g.Key,
                LastMessageAt = g.Max(m => m.CreatedAt),
                Unread = g.Count(m => m.Sender == ChatSender.Store && m.ReadAt == null)
            })
            .OrderByDescending(c => c.LastMessageAt)
            .Take(100)
            .ToListAsync(ct);

        if (conversations.Count == 0)
            return [];

        var orderIds = conversations.Select(c => c.OrderId).ToList();

        var orders = await db.Orders
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new
            {
                o.Id, o.OrderNumber,
                o.Store.Slug, o.Store.Name, o.Store.NameAr, o.Store.LogoPath
            })
            .ToDictionaryAsync(o => o.Id, ct);

        var lastMessages = await db.ChatMessages
            .Where(m => orderIds.Contains(m.OrderId))
            .GroupBy(m => m.OrderId)
            .Select(g => g.OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.OrderId, m.Body, m.Sender })
                .First())
            .ToDictionaryAsync(m => m.OrderId, ct);

        var result = new List<BuyerConversationDto>();
        foreach (var convo in conversations)
        {
            if (!orders.TryGetValue(convo.OrderId, out var order) ||
                !lastMessages.TryGetValue(convo.OrderId, out var last))
                continue;

            result.Add(new BuyerConversationDto(
                order.Id, order.OrderNumber, order.Slug, order.Name, order.NameAr,
                order.LogoPath, last.Body, last.Sender, convo.LastMessageAt, convo.Unread));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            result = result.Where(c =>
                c.StoreName.ToLowerInvariant().Contains(term) ||
                (c.StoreNameAr?.Contains(term) ?? false) ||
                c.OrderNumber.ToLowerInvariant().Contains(term)).ToList();
        }

        return result;
    }

    /// <summary>Unread store messages per order, for the buyer's badges.</summary>
    public async Task<Dictionary<Guid, int>> UnreadByOrderAsync(
        List<Guid> orderIds, CancellationToken ct = default)
    {
        if (orderIds.Count == 0)
            return [];

        return await db.ChatMessages
            .Where(m => orderIds.Contains(m.OrderId) && m.Sender == ChatSender.Store && m.ReadAt == null)
            .GroupBy(m => m.OrderId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    private Guid RequireUser() =>
        currentUser.UserId ?? throw new AuthFailedException("Not authenticated.");
}
