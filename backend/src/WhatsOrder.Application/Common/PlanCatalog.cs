using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Common;

/// <summary>null limit = unlimited.</summary>
public sealed record PlanDefinition(
    SubscriptionPlan Plan,
    int? MaxProducts,
    int? MaxOrdersPerMonth,
    bool WhatsAppNotifications,
    bool Analytics);

public static class PlanCatalog
{
    public static readonly PlanDefinition Free = new(SubscriptionPlan.Free,
        MaxProducts: 20, MaxOrdersPerMonth: 50, WhatsAppNotifications: false, Analytics: false);

    public static readonly PlanDefinition Pro = new(SubscriptionPlan.Pro,
        MaxProducts: null, MaxOrdersPerMonth: null, WhatsAppNotifications: true, Analytics: true);

    public static PlanDefinition For(SubscriptionPlan plan) =>
        plan == SubscriptionPlan.Pro ? Pro : Free;
}
