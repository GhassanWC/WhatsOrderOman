using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Chat;

public sealed record SendMessageRequest(string Body);

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Body)
            .Must(b => !string.IsNullOrWhiteSpace(b)).WithMessage("Message cannot be empty.")
            .MaximumLength(1000);
    }
}

/// <summary>
/// Per-order conversation between the customer and the store. Ownership is always
/// proven server-side: owners via the JWT tenant (IStoreContext), customers via the
/// slug + orderNumber + phone triple — the same proof as the public status endpoint.
/// Messages are persisted first; SignalR delivery is best-effort on top.
/// </summary>
public class ChatService(
    IAppDbContext db,
    IStoreContext storeContext,
    INotificationService notifications,
    IOrderEventBroadcaster broadcaster,
    ILogger<ChatService> logger)
{
    /// <summary>Newest conversations cap; older messages are never deleted, just not returned.</summary>
    private const int MaxMessages = 500;

    // ── Owner side ─────────────────────────────────────────────────────────

    /// <summary>Loads the conversation and marks the customer's messages as read.</summary>
    public async Task<List<ChatMessageDto>> GetOrderMessagesAsync(Guid orderId, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var order = await FindOwnedOrderAsync(store, orderId, ct);
        return await LoadAndMarkReadAsync(order, readerIs: ChatSender.Store, ct);
    }

    public async Task<ChatMessageDto> SendAsOwnerAsync(Guid orderId, string body, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var order = await FindOwnedOrderAsync(store, orderId, ct);
        return await SendAsync(store, order, ChatSender.Store, body, ct);
    }

    // ── Customer side (no accounts — proof is slug + orderNumber + phone) ──

    public async Task<List<ChatMessageDto>> GetPublicMessagesAsync(
        string slug, string orderNumber, string phone, CancellationToken ct = default)
    {
        var order = await FindPublicOrderAsync(slug, orderNumber, phone, ct);
        return await LoadAndMarkReadAsync(order, readerIs: ChatSender.Customer, ct);
    }

    public async Task<ChatMessageDto> SendAsCustomerAsync(
        string slug, string orderNumber, string phone, string body, CancellationToken ct = default)
    {
        var order = await FindPublicOrderAsync(slug, orderNumber, phone, ct);
        return await SendAsync(order.Store, order, ChatSender.Customer, body, ct);
    }

    // ── Buyer account side (proof = Order.BuyerUserId == authenticated user) ──

    public async Task<List<ChatMessageDto>> GetBuyerMessagesAsync(
        Guid userId, Guid orderId, CancellationToken ct = default)
    {
        var order = await FindBuyerOrderAsync(userId, orderId, ct);
        return await LoadAndMarkReadAsync(order, readerIs: ChatSender.Customer, ct);
    }

    public async Task<ChatMessageDto> SendAsBuyerAsync(
        Guid userId, Guid orderId, string body, CancellationToken ct = default)
    {
        var order = await FindBuyerOrderAsync(userId, orderId, ct);
        return await SendAsync(order.Store, order, ChatSender.Customer, body, ct);
    }

    // ── Shared ─────────────────────────────────────────────────────────────

    private async Task<ChatMessageDto> SendAsync(
        Store store, Order order, ChatSender sender, string body, CancellationToken ct)
    {
        var message = new ChatMessage
        {
            OrderId = order.Id,
            StoreId = store.Id,
            Sender = sender,
            Body = body.Trim()
        };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(ct);

        try
        {
            await notifications.NotifyNewMessageAsync(store, order, message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify about chat message on order {OrderNumber}", order.OrderNumber);
        }

        return message.ToDto();
    }

    /// <summary>
    /// Returns the conversation and marks the counterparty's messages read; the read
    /// receipt is broadcast so the sender's UI can flip to "seen" live.
    /// </summary>
    private async Task<List<ChatMessageDto>> LoadAndMarkReadAsync(
        Order order, ChatSender readerIs, CancellationToken ct)
    {
        var messages = await db.ChatMessages
            .Where(m => m.OrderId == order.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxMessages)
            .ToListAsync(ct);
        messages.Reverse();

        var counterparty = readerIs == ChatSender.Store ? ChatSender.Customer : ChatSender.Store;
        var unread = messages.Where(m => m.Sender == counterparty && m.ReadAt == null).ToList();
        if (unread.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var message in unread)
                message.ReadAt = now;
            await db.SaveChangesAsync(ct);

            try
            {
                await broadcaster.MessagesReadAsync(order.StoreId, new MessagesReadEvent(order.Id, readerIs, now), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to broadcast read receipt for order {OrderId}", order.Id);
            }
        }

        return messages.Select(m => m.ToDto()).ToList();
    }

    private async Task<Order> FindOwnedOrderAsync(Store store, Guid orderId, CancellationToken ct) =>
        await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.StoreId == store.Id, ct)
        ?? throw new NotFoundException("Order not found.");

    private async Task<Order> FindBuyerOrderAsync(Guid userId, Guid orderId, CancellationToken ct) =>
        await db.Orders
            .Include(o => o.Store)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerUserId == userId, ct)
        ?? throw new NotFoundException("Order not found.");

    private async Task<Order> FindPublicOrderAsync(
        string slug, string orderNumber, string phone, CancellationToken ct)
    {
        var normalizedPhone = PhoneNumber.Normalize(phone)
            ?? throw new NotFoundException("Order not found.");

        return await db.Orders
            .Include(o => o.Store)
            .Where(o => o.Store.Slug == Slugs.Normalize(slug)
                        && o.OrderNumber == orderNumber
                        && o.CustomerPhone == normalizedPhone)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Order not found.");
    }
}
