using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Orders;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Subscriptions;

public sealed record SubscriptionDto(
    string Plan,
    string Status,
    int? MaxProducts,
    int? MaxOrdersPerMonth,
    bool WhatsAppNotifications,
    bool Analytics,
    int ProductsUsed,
    int OrdersThisMonth,
    DateTime StartsAt);

public sealed record ChangePlanRequest(string Plan);

public class ChangePlanRequestValidator : AbstractValidator<ChangePlanRequest>
{
    public ChangePlanRequestValidator()
    {
        RuleFor(x => x.Plan)
            .Must(p => Enum.TryParse<SubscriptionPlan>(p, ignoreCase: true, out _))
            .WithMessage("Unknown plan.");
    }
}

public class SubscriptionService(IAppDbContext db, IStoreContext storeContext)
{
    public async Task<SubscriptionDto> GetAsync(CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        return await BuildDtoAsync(store.Id, store.Subscription.Plan, store.Subscription.Status,
            store.Subscription.StartsAt, ct);
    }

    /// <summary>
    /// MVP plan switch without payment. When a billing provider (Stripe / Lemon Squeezy)
    /// is added, this becomes the post-payment webhook handler's job.
    /// </summary>
    public async Task<SubscriptionDto> ChangePlanAsync(ChangePlanRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var plan = Enum.Parse<SubscriptionPlan>(request.Plan, ignoreCase: true);

        var subscription = store.Subscription;
        subscription.Plan = plan;
        subscription.Status = SubscriptionStatus.Active;
        subscription.StartsAt = DateTime.UtcNow;
        subscription.EndsAt = null;

        await db.SaveChangesAsync(ct);
        return await BuildDtoAsync(store.Id, plan, subscription.Status, subscription.StartsAt, ct);
    }

    private async Task<SubscriptionDto> BuildDtoAsync(
        Guid storeId, SubscriptionPlan plan, SubscriptionStatus status, DateTime startsAt, CancellationToken ct)
    {
        var limits = PlanCatalog.For(plan);
        var monthStart = OrderService.OmanMonthStartUtc(DateTime.UtcNow);

        return new SubscriptionDto(
            plan.ToString(),
            status.ToString(),
            limits.MaxProducts,
            limits.MaxOrdersPerMonth,
            limits.WhatsAppNotifications,
            limits.Analytics,
            ProductsUsed: await db.Products.CountAsync(p => p.StoreId == storeId, ct),
            OrdersThisMonth: await db.Orders.CountAsync(o => o.StoreId == storeId && o.CreatedAt >= monthStart, ct),
            startsAt);
    }
}
