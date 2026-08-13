using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WhatsOrder.Application.Account;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Infrastructure.Persistence;

namespace WhatsOrder.Infrastructure.Identity;

public class BuyerProfileService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    IFileStorage fileStorage) : IBuyerProfileService
{
    public async Task<BuyerProfileDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId);
        var profile = await db.BuyerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        return ToDto(user, profile);
    }

    public async Task<BuyerProfileDto> UpdateAsync(
        Guid userId, UpdateBuyerProfileRequest request, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId);

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            phone = PhoneNumber.Normalize(request.Phone)
                ?? throw new BusinessRuleException("invalid_phone", "Invalid phone number.");
        }

        user.DisplayName = request.DisplayName.Trim();
        user.PhoneNumber = phone;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new BusinessRuleException("identity",
                string.Join(" ", result.Errors.Select(e => e.Description)));

        var profile = await GetOrCreateProfileAsync(userId, ct);
        profile.PreferredLanguage = request.PreferredLanguage;
        profile.NotifyOrderUpdates = request.NotifyOrderUpdates;
        profile.NotifyMessages = request.NotifyMessages;
        profile.NotifyOffers = request.NotifyOffers;
        await db.SaveChangesAsync(ct);

        return ToDto(user, profile);
    }

    public async Task<BuyerProfileDto> SetAvatarAsync(
        Guid userId, string avatarPath, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId);
        var profile = await GetOrCreateProfileAsync(userId, ct);

        var previous = profile.AvatarPath;
        profile.AvatarPath = avatarPath;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previous))
            await fileStorage.DeleteAsync(previous, ct);

        return ToDto(user, profile);
    }

    private async Task<ApplicationUser> RequireUserAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
        ?? throw new AuthFailedException("Not authenticated.");

    private async Task<BuyerProfile> GetOrCreateProfileAsync(Guid userId, CancellationToken ct)
    {
        var profile = await db.BuyerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            profile = new BuyerProfile { UserId = userId };
            db.BuyerProfiles.Add(profile);
        }
        return profile;
    }

    private static BuyerProfileDto ToDto(ApplicationUser user, BuyerProfile? profile) => new(
        user.Email ?? "",
        user.DisplayName,
        user.PhoneNumber,
        profile?.AvatarPath,
        profile?.PreferredLanguage,
        profile?.NotifyOrderUpdates ?? true,
        profile?.NotifyMessages ?? true,
        profile?.NotifyOffers ?? true);
}
