namespace WhatsOrder.Application.Account;

/// <summary>
/// Buyer profile reads/writes. Implemented in Infrastructure because display name and
/// phone live on the Identity user, while preferences live in the BuyerProfile row.
/// </summary>
public interface IBuyerProfileService
{
    Task<BuyerProfileDto> GetAsync(Guid userId, CancellationToken ct = default);

    Task<BuyerProfileDto> UpdateAsync(Guid userId, UpdateBuyerProfileRequest request, CancellationToken ct = default);

    /// <summary>Stores the new avatar path and deletes the previous file, if any.</summary>
    Task<BuyerProfileDto> SetAvatarAsync(Guid userId, string avatarPath, CancellationToken ct = default);
}
