using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Offers;

public class OfferService(IAppDbContext db, IStoreContext storeContext)
{
    private const int MaxOffersPerStore = 20;

    // ── Owner ──────────────────────────────────────────────────────────────

    public async Task<List<OfferDto>> ListMineAsync(CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var offers = await db.Offers
            .Where(o => o.StoreId == store.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        return offers.Select(o => o.ToDto()).ToList();
    }

    public async Task<OfferDto> CreateAsync(SaveOfferRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);

        var count = await db.Offers.CountAsync(o => o.StoreId == store.Id, ct);
        if (count >= MaxOffersPerStore)
            throw new BusinessRuleException("offer_limit",
                $"You can create up to {MaxOffersPerStore} offers.");

        var offer = new Offer { StoreId = store.Id };
        Apply(offer, request);
        db.Offers.Add(offer);
        await db.SaveChangesAsync(ct);
        return offer.ToDto();
    }

    public async Task<OfferDto> UpdateAsync(Guid id, SaveOfferRequest request, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var offer = await db.Offers.FirstOrDefaultAsync(o => o.Id == id && o.StoreId == store.Id, ct)
            ?? throw new NotFoundException("Offer not found.");

        Apply(offer, request);
        await db.SaveChangesAsync(ct);
        return offer.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var store = await storeContext.GetMyStoreAsync(ct);
        var offer = await db.Offers.FirstOrDefaultAsync(o => o.Id == id && o.StoreId == store.Id, ct)
            ?? throw new NotFoundException("Offer not found.");

        db.Offers.Remove(offer);
        await db.SaveChangesAsync(ct);
    }

    // ── Public ─────────────────────────────────────────────────────────────

    /// <summary>Currently running offers of one store.</summary>
    public async Task<List<PublicOfferDto>> GetRunningForStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var offers = await db.Offers
            .Include(o => o.Store)
            .Where(o => o.StoreId == storeId && o.IsActive
                        && o.StartsAt <= now && (o.EndsAt == null || o.EndsAt > now))
            .OrderBy(o => o.EndsAt == null).ThenBy(o => o.EndsAt)
            .ToListAsync(ct);
        return offers.Select(o => o.ToPublicDto(o.Store)).ToList();
    }

    /// <summary>Currently running offers across the marketplace, soonest-ending first.</summary>
    public async Task<List<PublicOfferDto>> GetRunningAsync(int count = 12, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var offers = await db.Offers
            .Include(o => o.Store)
            .Where(o => o.IsActive && o.StartsAt <= now && (o.EndsAt == null || o.EndsAt > now))
            .OrderBy(o => o.EndsAt == null).ThenBy(o => o.EndsAt)
            .Take(Math.Clamp(count, 1, 50))
            .ToListAsync(ct);
        return offers.Select(o => o.ToPublicDto(o.Store)).ToList();
    }

    private static void Apply(Offer offer, SaveOfferRequest request)
    {
        offer.Title = request.Title.Trim();
        offer.TitleAr = Clean(request.TitleAr);
        offer.Description = Clean(request.Description);
        offer.DescriptionAr = Clean(request.DescriptionAr);
        offer.Type = request.Type;
        offer.DiscountValue = request.Type == Domain.Enums.OfferType.FreeDelivery
            ? 0 : Money.Round(request.DiscountValue);
        offer.MinimumOrderAmount = Money.Round(request.MinimumOrderAmount);
        offer.StartsAt = DateTime.SpecifyKind(request.StartsAt, DateTimeKind.Utc);
        offer.EndsAt = request.EndsAt is { } ends ? DateTime.SpecifyKind(ends, DateTimeKind.Utc) : null;
        offer.IsActive = request.IsActive;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
