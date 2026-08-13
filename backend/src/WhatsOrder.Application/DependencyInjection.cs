using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WhatsOrder.Application.Account;
using WhatsOrder.Application.Activity;
using WhatsOrder.Application.Categories;
using WhatsOrder.Application.Chat;
using WhatsOrder.Application.Common;
using WhatsOrder.Application.Dashboard;
using WhatsOrder.Application.Marketplace;
using WhatsOrder.Application.Offers;
using WhatsOrder.Application.Orders;
using WhatsOrder.Application.Products;
using WhatsOrder.Application.Public;
using WhatsOrder.Application.Realtime;
using WhatsOrder.Application.Recommendations;
using WhatsOrder.Application.Reviews;
using WhatsOrder.Application.Stores;
using WhatsOrder.Application.Subscriptions;
using WhatsOrder.Application.WhatsApp;

namespace WhatsOrder.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IStoreContext, StoreContext>();
        services.AddScoped<StoreService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<ProductService>();
        services.AddScoped<OrderService>();
        services.AddScoped<PublicCatalogService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<ChatService>();
        services.AddScoped<NotificationInbox>();
        services.AddScoped<INotificationService, NotificationService>();

        // Marketplace / buyer account.
        services.AddScoped<ActivityTracker>();
        services.AddScoped<AddressService>();
        services.AddScoped<FavoritesService>();
        services.AddScoped<BuyerOrderService>();
        services.AddScoped<AccountService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<OfferService>();
        services.AddScoped<MarketplaceCardFactory>();
        services.AddScoped<MarketplaceService>();
        services.AddScoped<IRecommendationService, RecommendationService>();

        // WhatsApp is one optional channel behind the notification abstraction —
        // core ordering works with zero WhatsApp configuration. SMS would register here too.
        services.AddScoped<WhatsAppNotifier>();
        services.AddScoped<IOrderChannelNotifier>(sp => sp.GetRequiredService<WhatsAppNotifier>());

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        return services;
    }
}
