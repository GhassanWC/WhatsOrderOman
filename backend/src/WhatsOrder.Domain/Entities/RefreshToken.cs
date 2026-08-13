using WhatsOrder.Domain.Common;

namespace WhatsOrder.Domain.Entities;

/// <summary>Rotating refresh token. Only the SHA-256 hash is stored.</summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash of the token that replaced this one (rotation chain, reuse detection).</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
