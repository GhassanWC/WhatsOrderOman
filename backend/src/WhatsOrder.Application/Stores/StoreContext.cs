using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Stores;

/// <summary>
/// Per-request tenant resolution: JWT user id → Store.OwnerId. Client-supplied store ids
/// are never accepted anywhere in the API.
/// </summary>
public class StoreContext(IAppDbContext db, ICurrentUser currentUser) : IStoreContext
{
    private Store? _store;
    private bool _loaded;

    public async Task<Store?> FindMyStoreAsync(CancellationToken ct = default)
    {
        if (_loaded)
            return _store;

        if (currentUser.UserId is not { } userId)
            throw new AuthFailedException("Not authenticated.");

        _store = await db.Stores
            .Include(s => s.Settings)
            .Include(s => s.Subscription)
            .FirstOrDefaultAsync(s => s.OwnerId == userId, ct);
        _loaded = true;
        return _store;
    }

    public async Task<Store> GetMyStoreAsync(CancellationToken ct = default) =>
        await FindMyStoreAsync(ct)
        ?? throw new NotFoundException("You have not created a store yet.");
}
