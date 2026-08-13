using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsOrder.Application.Activity;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Orders;

public class OrderService(
    IAppDbContext db,
    IStoreContext storeContext,
    INotificationService notifications,
    ICurrentUser currentUser,
    ActivityTracker activity,
    ILogger<OrderService> logger)
{
    // ── Public checkout ────────────────────────────────────────────────────

    public async Task<PublicOrderCreatedDto> CreatePublicOrderAsync(
        string slug, CreatePublicOrderRequest request, CancellationToken ct = default)
    {
        var store = await db.Stores
            .Include(s => s.Settings)
            .Include(s => s.Subscription)
            .FirstOrDefaultAsync(s => s.Slug == Slugs.Normalize(slug), ct)
            ?? throw new NotFoundException("Store not found.");

        if (!store.IsAcceptingOrders)
            throw new BusinessRuleException("store_closed", "This store is not accepting orders right now.");

        var limits = PlanCatalog.For(store.Subscription.Plan);
        if (limits.MaxOrdersPerMonth is { } maxOrders)
        {
            var monthStartUtc = OmanMonthStartUtc(DateTime.UtcNow);
            var monthCount = await db.Orders.CountAsync(o => o.StoreId == store.Id && o.CreatedAt >= monthStartUtc, ct);
            if (monthCount >= maxOrders)
                throw new BusinessRuleException("store_order_limit",
                    "This store cannot accept more orders right now. Please try again later.");
        }

        switch (request.FulfillmentMethod)
        {
            case FulfillmentMethod.Delivery when !store.Settings.DeliveryEnabled:
                throw new BusinessRuleException("delivery_disabled", "This store does not offer delivery.");
            case FulfillmentMethod.Pickup when !store.Settings.PickupEnabled:
                throw new BusinessRuleException("pickup_disabled", "This store does not offer pickup.");
        }

        var phone = PhoneNumber.Normalize(request.CustomerPhone)
            ?? throw new BusinessRuleException("invalid_phone", "Invalid phone number.");

        var lang = store.Settings.DefaultLanguage;

        // Load every referenced product with variants — only from this store.
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Include(p => p.Variants).ThenInclude(v => v.Options)
            .Where(p => p.StoreId == store.Id && productIds.Contains(p.Id))
            .AsSplitQuery()
            .ToDictionaryAsync(p => p.Id, ct);

        var order = new Order
        {
            StoreId = store.Id,
            // Signed-in customers get the order attached to their marketplace account.
            BuyerUserId = currentUser.UserId,
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = phone,
            FulfillmentMethod = request.FulfillmentMethod,
            DeliveryAddress = request.FulfillmentMethod == FulfillmentMethod.Delivery
                ? request.DeliveryAddress?.Trim() : null,
            GoogleMapsUrl = Clean(request.GoogleMapsUrl),
            PreferredTime = Clean(request.PreferredTime),
            Notes = Clean(request.Notes),
            Status = OrderStatus.New
        };

        decimal subtotal = 0, discount = 0;
        var stockNeeded = new Dictionary<Guid, int>();

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !product.IsAvailable)
                throw new BusinessRuleException("product_unavailable",
                    "One of the products in your cart is no longer available. Please refresh and try again.");

            var (unitPrice, originalUnitPrice, variantsText) =
                PriceItem(product, item.OptionIds ?? [], lang);

            var lineTotal = Money.Round(unitPrice * item.Quantity);
            subtotal += lineTotal;
            discount += Money.Round((originalUnitPrice - unitPrice) * item.Quantity);

            if (product.TracksStock)
                stockNeeded[product.Id] = stockNeeded.GetValueOrDefault(product.Id) + item.Quantity;

            order.Items.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = lang == "ar" && !string.IsNullOrWhiteSpace(product.NameAr) ? product.NameAr! : product.Name,
                VariantsText = variantsText,
                UnitPrice = unitPrice,
                Quantity = item.Quantity,
                LineTotal = lineTotal
            });
        }

        foreach (var (productId, needed) in stockNeeded)
        {
            var product = products[productId];
            if (product.StockQuantity < needed)
                throw new BusinessRuleException("insufficient_stock",
                    $"Not enough stock for \"{product.Name}\" — only {product.StockQuantity} left.");
            product.StockQuantity -= needed;
        }

        order.Subtotal = Money.Round(subtotal);
        order.Discount = Money.Round(discount);
        order.DeliveryFee = request.FulfillmentMethod == FulfillmentMethod.Delivery
            ? store.Settings.DeliveryFee : 0;
        order.Total = Money.Round(order.Subtotal + order.DeliveryFee);

        if (store.Settings.MinimumOrderAmount > 0 && order.Subtotal < store.Settings.MinimumOrderAmount)
            throw new BusinessRuleException("min_order",
                $"The minimum order for this store is {Money.Format(store.Settings.MinimumOrderAmount)}.");

        var appliedOffer = await ApplyBestOfferAsync(store.Id, order, ct);

        await UpsertCustomerAsync(store.Id, order, ct);

        // The initial timeline entry is part of the same transaction as the order.
        order.StatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            PreviousStatus = null,
            NewStatus = OrderStatus.New,
            ChangedBy = "customer"
        });

        // Per-store sequential number; the unique index (StoreId, OrderNumber) guards races.
        store.OrderSequence++;
        order.OrderNumber = $"WO-{store.OrderSequence}";
        db.Orders.Add(order);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BusinessRuleException("store_busy",
                    "The store is receiving many orders right now — please try again.");
            }
            catch (DbUpdateException) when (attempt < 5)
            {
                store.OrderSequence++;
                order.OrderNumber = $"WO-{store.OrderSequence}";
            }
        }

        try
        {
            await notifications.NotifyNewOrderAsync(store, order, ct);
        }
        catch (Exception ex)
        {
            // Never fail a saved order because notifications could not be delivered.
            logger.LogError(ex, "Failed to send notifications for order {OrderNumber}", order.OrderNumber);
        }

        await activity.TrackAsync(BuyerEventType.Ordered, order.BuyerUserId, storeId: store.Id, ct: ct);

        return new PublicOrderCreatedDto(
            order.OrderNumber, order.Subtotal, order.DeliveryFee, order.Discount,
            order.Total, order.Status, store.WhatsAppNumber,
            appliedOffer?.Title, appliedOffer?.TitleAr);
    }

    /// <summary>
    /// Applies the single best-value running store offer. Percentage/fixed offers land in
    /// Discount and reduce the total; free delivery zeroes the fee. The subtotal itself is
    /// never modified, so the minimum-order rule stays based on what the customer ordered.
    /// </summary>
    private async Task<Offer?> ApplyBestOfferAsync(Guid storeId, Order order, CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;
        var candidates = await db.Offers
            .Where(o => o.StoreId == storeId && o.IsActive
                        && o.StartsAt <= utcNow && (o.EndsAt == null || o.EndsAt > utcNow)
                        && order.Subtotal >= o.MinimumOrderAmount)
            .ToListAsync(ct);

        Offer? best = null;
        decimal bestValue = 0;
        foreach (var candidate in candidates)
        {
            var value = candidate.Type switch
            {
                OfferType.Percentage =>
                    Money.Round(order.Subtotal * Math.Clamp(candidate.DiscountValue, 0, 100) / 100m),
                OfferType.FixedAmount => Math.Min(Money.Round(candidate.DiscountValue), order.Subtotal),
                OfferType.FreeDelivery => order.DeliveryFee,
                _ => 0m
            };
            if (value > bestValue)
            {
                bestValue = value;
                best = candidate;
            }
        }

        if (best is null || bestValue <= 0)
            return null;

        if (best.Type == OfferType.FreeDelivery)
        {
            order.DeliveryFee = 0;
        }
        else
        {
            order.Discount = Money.Round(order.Discount + bestValue);
        }
        order.Total = Money.Round(order.Subtotal + order.DeliveryFee
                                  - (best.Type == OfferType.FreeDelivery ? 0 : bestValue));
        return best;
    }

    /// <summary>Charged price = (discounted ?? price) + option adjustments. Never trusts client prices.</summary>
    public static (decimal UnitPrice, decimal OriginalUnitPrice, string? VariantsText) PriceItem(
        Product product, IReadOnlyList<Guid> optionIds, string lang)
    {
        var selectedByVariant = new Dictionary<Guid, ProductVariantOption>();
        foreach (var optionId in optionIds.Distinct())
        {
            var variant = product.Variants.FirstOrDefault(v => v.Options.Any(o => o.Id == optionId))
                ?? throw new BusinessRuleException("invalid_option",
                    "One of the selected options is not valid for this product.");
            var option = variant.Options.First(o => o.Id == optionId);

            if (!option.IsAvailable)
                throw new BusinessRuleException("option_unavailable",
                    $"The option \"{option.Name}\" is currently unavailable.");
            if (!selectedByVariant.TryAdd(variant.Id, option))
                throw new BusinessRuleException("duplicate_option",
                    $"Choose only one option for \"{variant.Name}\".");
        }

        foreach (var variant in product.Variants.Where(v => v.IsRequired))
        {
            if (variant.Options.Any(o => o.IsAvailable) && !selectedByVariant.ContainsKey(variant.Id))
                throw new BusinessRuleException("variant_required",
                    $"Please choose an option for \"{variant.Name}\".");
        }

        var adjustments = selectedByVariant.Values.Sum(o => o.PriceAdjustment);
        var unitPrice = Math.Max(0, Money.Round(product.EffectiveBasePrice + adjustments));
        var originalUnitPrice = Math.Max(0, Money.Round(product.Price + adjustments));

        string? variantsText = null;
        if (selectedByVariant.Count > 0)
        {
            variantsText = string.Join(" • ", product.Variants
                .OrderBy(v => v.SortOrder)
                .Where(v => selectedByVariant.ContainsKey(v.Id))
                .Select(v =>
                {
                    var option = selectedByVariant[v.Id];
                    var variantName = lang == "ar" && !string.IsNullOrWhiteSpace(v.NameAr) ? v.NameAr : v.Name;
                    var optionName = lang == "ar" && !string.IsNullOrWhiteSpace(option.NameAr) ? option.NameAr : option.Name;
                    return $"{variantName}: {optionName}";
                }));
        }

        return (unitPrice, originalUnitPrice, variantsText);
    }

    public async Task<PublicOrderStatusDto> GetPublicOrderAsync(
        string slug, string orderNumber, string phone, CancellationToken ct = default)
    {
        var normalizedPhone = PhoneNumber.Normalize(phone)
            ?? throw new NotFoundException("Order not found.");

        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .AsSplitQuery()
            .Where(o => o.Store.Slug == Slugs.Normalize(slug)
                        && o.OrderNumber == orderNumber
                        && o.CustomerPhone == normalizedPhone)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Order not found.");

        return new PublicOrderStatusDto(
            order.Id, order.OrderNumber, order.Status, order.FulfillmentMethod,
            order.Subtotal, order.DeliveryFee, order.Total,
            order.Items.Select(i => i.ToDto()).ToList(),
            order.EstimatedReadyAt,
            order.StatusHistory.OrderBy(h => h.CreatedAt).Select(h => h.ToDto()).ToList(),
            order.CreatedAt);
    }

    // ── Owner order management ─────────────────────────────────────────────

    public async Task<OrdersPage> GetOrdersAsync(
        string? filter, string? search, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Orders.Where(o => o.StoreId == store.Id);

        query = (filter ?? "all").ToLowerInvariant() switch
        {
            "today" => query.Where(o => o.CreatedAt >= OmanDayStartUtc(DateTime.UtcNow)),
            "new" => query.Where(o => o.Status == OrderStatus.New),
            "confirmed" => query.Where(o => o.Status == OrderStatus.Confirmed),
            "preparing" => query.Where(o => o.Status == OrderStatus.Preparing),
            "ready" => query.Where(o => o.Status == OrderStatus.Ready),
            "outfordelivery" => query.Where(o => o.Status == OrderStatus.OutForDelivery),
            "completed" => query.Where(o => o.Status == OrderStatus.Completed),
            "cancelled" => query.Where(o => o.Status == OrderStatus.Cancelled),
            "rejected" => query.Where(o => o.Status == OrderStatus.Rejected),
            "active" => query.Where(o => o.Status != OrderStatus.Completed
                                         && o.Status != OrderStatus.Cancelled
                                         && o.Status != OrderStatus.Rejected),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.CustomerName.ToLower().Contains(term) ||
                o.CustomerPhone.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var newCount = await db.Orders.CountAsync(o => o.StoreId == store.Id && o.Status == OrderStatus.New, ct);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var unread = await UnreadByOrderAsync(orders.Select(o => o.Id).ToList(), ct);

        return new OrdersPage(
            orders.Select(o => o.ToListItemDto(unread.GetValueOrDefault(o.Id))).ToList(),
            total, page, pageSize, newCount);
    }

    public async Task<OrderDto> GetOrderAsync(Guid id, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id && o.StoreId == store.Id, ct)
            ?? throw new NotFoundException("Order not found.");

        var unread = await UnreadByOrderAsync([order.Id], ct);
        return order.ToDto(unread.GetValueOrDefault(order.Id));
    }

    public async Task<OrderDto> UpdateStatusAsync(Guid id, UpdateOrderStatusRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id && o.StoreId == store.Id, ct)
            ?? throw new NotFoundException("Order not found.");

        var newStatus = request.Status;
        if (!OrderStatusRules.CanTransition(order.Status, newStatus, order.FulfillmentMethod))
            throw new BusinessRuleException("invalid_transition",
                $"Cannot change order status from {order.Status} to {newStatus}.");

        if (newStatus is OrderStatus.Cancelled or OrderStatus.Rejected)
            await RestockAsync(order, ct);

        var previousStatus = order.Status;
        order.Status = newStatus;
        if (request.EstimatedMinutes is { } minutes)
            order.EstimatedReadyAt = DateTime.UtcNow.AddMinutes(minutes);

        // History entry rides the same SaveChanges as the status itself — atomic.
        // Explicit DbSet.Add: BaseEntity pre-generates Guid keys, so an entity merely
        // discovered via the tracked order's navigation would be treated as Modified.
        var historyEntry = new OrderStatusHistory
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedBy = "store"
        };
        order.StatusHistory.Add(historyEntry);
        db.OrderStatusHistory.Add(historyEntry);
        await db.SaveChangesAsync(ct);

        try
        {
            await notifications.NotifyOrderStatusChangedAsync(store, order, previousStatus, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send status notifications for order {OrderNumber}", order.OrderNumber);
        }

        var unread = await UnreadByOrderAsync([order.Id], ct);
        return order.ToDto(unread.GetValueOrDefault(order.Id));
    }

    /// <summary>Unread customer messages per order, for the owner's badges.</summary>
    private async Task<Dictionary<Guid, int>> UnreadByOrderAsync(List<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0)
            return [];

        return await db.ChatMessages
            .Where(m => orderIds.Contains(m.OrderId) && m.Sender == ChatSender.Customer && m.ReadAt == null)
            .GroupBy(m => m.OrderId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    private async Task RestockAsync(Order order, CancellationToken ct)
    {
        var productIds = order.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).ToList();
        if (productIds.Count == 0)
            return;

        var products = await db.Products
            .IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var item in order.Items)
        {
            if (item.ProductId.HasValue
                && products.TryGetValue(item.ProductId.Value, out var product)
                && product.TracksStock)
            {
                product.StockQuantity += item.Quantity;
            }
        }
    }

    private async Task UpsertCustomerAsync(Guid storeId, Order order, CancellationToken ct)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.StoreId == storeId && c.Phone == order.CustomerPhone, ct);

        var now = DateTime.UtcNow;
        if (customer is null)
        {
            customer = new Customer
            {
                StoreId = storeId,
                Name = order.CustomerName,
                Phone = order.CustomerPhone,
                FirstOrderAt = now
            };
            db.Customers.Add(customer);
        }

        customer.Name = order.CustomerName;
        customer.OrdersCount++;
        customer.TotalSpent += order.Total;
        customer.LastOrderAt = now;
        order.Customer = customer;
    }

    /// <summary>Start of "today" in Oman (UTC+4, no DST) expressed in UTC.</summary>
    public static DateTime OmanDayStartUtc(DateTime utcNow)
    {
        var local = utcNow + OpeningHours.OmanUtcOffset;
        return DateTime.SpecifyKind(local.Date - OpeningHours.OmanUtcOffset, DateTimeKind.Utc);
    }

    /// <summary>Start of the current month in Oman expressed in UTC.</summary>
    public static DateTime OmanMonthStartUtc(DateTime utcNow)
    {
        var local = utcNow + OpeningHours.OmanUtcOffset;
        return DateTime.SpecifyKind(new DateTime(local.Year, local.Month, 1) - OpeningHours.OmanUtcOffset, DateTimeKind.Utc);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
