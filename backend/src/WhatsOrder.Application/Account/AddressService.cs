using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;

namespace WhatsOrder.Application.Account;

public class AddressService(IAppDbContext db, ICurrentUser currentUser)
{
    private const int MaxAddresses = 10;

    public async Task<List<BuyerAddressDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = RequireUser();
        var addresses = await db.BuyerAddresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return addresses.Select(a => a.ToDto()).ToList();
    }

    public async Task<BuyerAddressDto> CreateAsync(SaveAddressRequest request, CancellationToken ct = default)
    {
        var userId = RequireUser();

        var count = await db.BuyerAddresses.CountAsync(a => a.UserId == userId, ct);
        if (count >= MaxAddresses)
            throw new BusinessRuleException("address_limit",
                $"You can save up to {MaxAddresses} addresses.");

        var address = new BuyerAddress { UserId = userId };
        Apply(address, request);

        // First address is always the default.
        address.IsDefault = request.IsDefault || count == 0;
        if (address.IsDefault)
            await ClearDefaultAsync(userId, ct);

        db.BuyerAddresses.Add(address);
        await db.SaveChangesAsync(ct);
        return address.ToDto();
    }

    public async Task<BuyerAddressDto> UpdateAsync(Guid id, SaveAddressRequest request, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var address = await FindOwnedAsync(userId, id, ct);

        Apply(address, request);
        if (request.IsDefault && !address.IsDefault)
        {
            await ClearDefaultAsync(userId, ct);
            address.IsDefault = true;
        }

        await db.SaveChangesAsync(ct);
        return address.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var address = await FindOwnedAsync(userId, id, ct);
        var wasDefault = address.IsDefault;

        db.BuyerAddresses.Remove(address);
        await db.SaveChangesAsync(ct);

        if (wasDefault)
        {
            var next = await db.BuyerAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (next is not null)
            {
                next.IsDefault = true;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task<BuyerAddressDto> SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        var userId = RequireUser();
        var address = await FindOwnedAsync(userId, id, ct);

        await ClearDefaultAsync(userId, ct);
        address.IsDefault = true;
        await db.SaveChangesAsync(ct);
        return address.ToDto();
    }

    private void Apply(BuyerAddress address, SaveAddressRequest request)
    {
        address.Label = request.Label.Trim();
        address.RecipientName = request.RecipientName.Trim();
        address.Phone = PhoneNumber.Normalize(request.Phone)
            ?? throw new BusinessRuleException("invalid_phone", "Invalid phone number.");
        address.Governorate = Clean(request.Governorate);
        address.Wilayat = Clean(request.Wilayat);
        address.City = Clean(request.City);
        address.Area = Clean(request.Area);
        address.Street = Clean(request.Street);
        address.Building = Clean(request.Building);
        address.Apartment = Clean(request.Apartment);
        address.Notes = Clean(request.Notes);
        address.Latitude = request.Latitude;
        address.Longitude = request.Longitude;
    }

    private async Task ClearDefaultAsync(Guid userId, CancellationToken ct)
    {
        var defaults = await db.BuyerAddresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ToListAsync(ct);
        foreach (var address in defaults)
            address.IsDefault = false;
    }

    private async Task<BuyerAddress> FindOwnedAsync(Guid userId, Guid id, CancellationToken ct) =>
        await db.BuyerAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
        ?? throw new NotFoundException("Address not found.");

    private Guid RequireUser() =>
        currentUser.UserId ?? throw new AuthFailedException("Not authenticated.");

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class AddressMapping
{
    public static BuyerAddressDto ToDto(this BuyerAddress a) => new(
        a.Id, a.Label, a.RecipientName, a.Phone, a.Governorate, a.Wilayat, a.City,
        a.Area, a.Street, a.Building, a.Apartment, a.Notes, a.Latitude, a.Longitude,
        a.IsDefault, a.CreatedAt);
}
