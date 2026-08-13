using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Enums;

namespace WhatsOrder.Application.Account;

/// <summary>Composes the /account overview from the buyer's own data only.</summary>
public class AccountService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IBuyerProfileService profiles,
    BuyerOrderService buyerOrders)
{
    public async Task<AccountOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new AuthFailedException("Not authenticated.");

        var profile = await profiles.GetAsync(userId, ct);
        var recent = await buyerOrders.ListAsync("all", page: 1, pageSize: 3, ct);

        var unreadMessages = await db.ChatMessages.CountAsync(m =>
            db.Orders.Any(o => o.Id == m.OrderId && o.BuyerUserId == userId) &&
            m.Sender == ChatSender.Store && m.ReadAt == null, ct);

        var unreadNotifications = await db.Notifications.CountAsync(
            n => n.UserId == userId && !n.IsRead, ct);

        var favoriteStores = await db.FavoriteStores.CountAsync(f => f.UserId == userId, ct);
        var favoriteProducts = await db.FavoriteProducts.CountAsync(f => f.UserId == userId, ct);

        var defaultAddress = await db.BuyerAddresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .FirstOrDefaultAsync(ct);

        return new AccountOverviewDto(
            profile,
            recent.ActiveCount,
            unreadMessages,
            unreadNotifications,
            favoriteStores,
            favoriteProducts,
            recent.Items,
            defaultAddress?.ToDto());
    }
}
