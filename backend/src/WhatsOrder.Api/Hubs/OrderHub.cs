using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Api.Hubs;

/// <summary>
/// Real-time transport only — no business logic lives here. The hub authenticates
/// connections, manages group membership and forwards typing signals; every event
/// that matters is persisted by the services before being broadcast.
///
/// Groups:
///   store:{storeId} — the owner's dashboard (joined automatically from the JWT tenant).
///   buyer:{userId}  — a signed-in user's personal channel (account badges, order updates).
///   order:{orderId} — one order's conversation (customer joins via the same
///                     slug+orderNumber+phone proof as the public REST endpoints, a
///                     buyer via ?orderId= on their own order; the owner via WatchOrder).
/// </summary>
public class OrderHub(IAppDbContext db) : Hub
{
    private const string StoreIdKey = "storeId";
    private const string OrderIdKey = "orderId";
    private const string SenderKey = "sender";
    private const string UserIdKey = "userId";

    public static string StoreGroup(Guid storeId) => $"store:{storeId}";
    public static string OrderGroup(Guid orderId) => $"order:{orderId}";
    public static string BuyerGroup(Guid userId) => $"buyer:{userId}";

    public override async Task OnConnectedAsync()
    {
        // Authenticated connection — JWT (validated by the bearer middleware).
        // Client-supplied ids are never trusted: the store comes from the token's
        // tenant, and a requested order must belong to the token's user.
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var sub = Context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sub, out var userId))
            {
                Context.Abort();
                return;
            }

            Context.Items[UserIdKey] = userId;
            await Groups.AddToGroupAsync(Context.ConnectionId, BuyerGroup(userId));

            // Buyer opening one of their own order conversations (?orderId=).
            var requestedOrder = Context.GetHttpContext()?.Request.Query["orderId"].ToString();
            if (Guid.TryParse(requestedOrder, out var buyerOrderId))
            {
                var owns = await db.Orders.AnyAsync(
                    o => o.Id == buyerOrderId && o.BuyerUserId == userId, Context.ConnectionAborted);
                if (!owns)
                {
                    Context.Abort();
                    return;
                }

                Context.Items[OrderIdKey] = buyerOrderId;
                Context.Items[SenderKey] = ChatSender.Customer;
                await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroup(buyerOrderId));
                await base.OnConnectedAsync();
                return;
            }

            // Owner dashboard connection when the user has a store; plain buyer otherwise.
            var storeId = await db.Stores
                .Where(s => s.OwnerId == userId)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(Context.ConnectionAborted);

            if (storeId is { } id)
            {
                Context.Items[StoreIdKey] = id;
                Context.Items[SenderKey] = ChatSender.Store;
                await Groups.AddToGroupAsync(Context.ConnectionId, StoreGroup(id));
            }
            else
            {
                Context.Items[SenderKey] = ChatSender.Customer;
            }

            await base.OnConnectedAsync();
            return;
        }

        // Customer connection — anonymous, proven by slug + orderNumber + phone,
        // exactly like the public order-status endpoint.
        var query = Context.GetHttpContext()?.Request.Query;
        var slug = query?["slug"].ToString();
        var orderNumber = query?["orderNumber"].ToString();
        var phone = PhoneNumber.Normalize(query?["phone"].ToString());

        if (!string.IsNullOrWhiteSpace(slug) && !string.IsNullOrWhiteSpace(orderNumber) && phone is not null)
        {
            var order = await db.Orders
                .Where(o => o.Store.Slug == Slugs.Normalize(slug)
                            && o.OrderNumber == orderNumber
                            && o.CustomerPhone == phone)
                .Select(o => new { o.Id })
                .FirstOrDefaultAsync(Context.ConnectionAborted);

            if (order is not null)
            {
                Context.Items[OrderIdKey] = order.Id;
                Context.Items[SenderKey] = ChatSender.Customer;
                await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroup(order.Id));
                await base.OnConnectedAsync();
                return;
            }
        }

        Context.Abort();
    }

    /// <summary>
    /// Health probe. Invocations are processed only after OnConnectedAsync completes,
    /// so a Ping round-trip also guarantees group membership is in place.
    /// </summary>
    public string Ping() => "pong";

    /// <summary>Owner opens an order's chat — join its group for typing/read events.</summary>
    public async Task WatchOrder(Guid orderId)
    {
        var storeId = RequireStore();
        var belongs = await db.Orders.AnyAsync(
            o => o.Id == orderId && o.StoreId == storeId, Context.ConnectionAborted);
        if (!belongs)
            throw new HubException("Order not found.");

        await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroup(orderId));
    }

    public async Task UnwatchOrder(Guid orderId)
    {
        RequireStore();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, OrderGroup(orderId));
    }

    /// <summary>Buyer opens one of their own conversations (account messages inbox).</summary>
    public async Task WatchMyOrder(Guid orderId)
    {
        var userId = RequireUser();
        var owns = await db.Orders.AnyAsync(
            o => o.Id == orderId && o.BuyerUserId == userId, Context.ConnectionAborted);
        if (!owns)
            throw new HubException("Order not found.");

        Context.Items[OrderIdKey] = orderId;
        Context.Items[SenderKey] = ChatSender.Customer;
        await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroup(orderId));
    }

    public async Task UnwatchMyOrder(Guid orderId)
    {
        RequireUser();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, OrderGroup(orderId));
    }

    /// <summary>Ephemeral typing indicator — intentionally not persisted.</summary>
    public async Task Typing(Guid orderId, bool isTyping)
    {
        var sender = (ChatSender)Context.Items[SenderKey]!;

        // A connection may only signal on an order it is authorized for.
        if (sender == ChatSender.Customer)
        {
            if (Context.Items[OrderIdKey] is not Guid watchedOrderId || watchedOrderId != orderId)
                throw new HubException("Not your order.");
        }
        else
        {
            var storeId = RequireStore();
            var belongs = await db.Orders.AnyAsync(
                o => o.Id == orderId && o.StoreId == storeId, Context.ConnectionAborted);
            if (!belongs)
                throw new HubException("Order not found.");
        }

        await Clients.OthersInGroup(OrderGroup(orderId))
            .SendAsync("typing", new { orderId, sender, isTyping });
    }

    private Guid RequireStore() =>
        Context.Items[StoreIdKey] is Guid storeId
            ? storeId
            : throw new HubException("Owner connection required.");

    private Guid RequireUser() =>
        Context.Items[UserIdKey] is Guid userId
            ? userId
            : throw new HubException("Authenticated connection required.");
}
