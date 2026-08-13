using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;

namespace WhatsOrder.Application.Realtime;

/// <summary>
/// The owner's persisted notification list (the bell). Read side of notifications;
/// writing happens in <see cref="NotificationService"/>. Scoped to the JWT user —
/// client-supplied ids are never trusted.
/// </summary>
public class NotificationInbox(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<NotificationsPage> ListAsync(int limit = 30, CancellationToken ct = default)
    {
        var userId = RequireUser();
        limit = Math.Clamp(limit, 1, 100);

        var items = await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.OrderId, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);

        var unreadCount = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return new NotificationsPage(items, unreadCount);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        var unread = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);
        foreach (var notification in unread)
            notification.IsRead = true;
        await db.SaveChangesAsync(ct);
        return unread.Count;
    }

    private Guid RequireUser() =>
        currentUser.UserId ?? throw new AuthFailedException("Not authenticated.");
}
