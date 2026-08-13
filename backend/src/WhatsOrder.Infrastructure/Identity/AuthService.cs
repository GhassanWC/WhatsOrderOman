using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsOrder.Application.Auth;
using WhatsOrder.Application.Common;
using WhatsOrder.Domain.Entities;
using WhatsOrder.Infrastructure.Common;
using WhatsOrder.Infrastructure.Persistence;

namespace WhatsOrder.Infrastructure.Identity;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    JwtTokenService jwt,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("An account with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim()
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new BusinessRuleException("identity",
                string.Join(" ", result.Errors.Select(e => e.Description)));

        // Sellers also get the Buyer role so they can shop the marketplace with one account.
        if (request.AccountType == "buyer")
            await userManager.AddToRolesAsync(user, ["Buyer"]);
        else
            await userManager.AddToRolesAsync(user, ["Owner", "Buyer"]);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant())
            ?? throw new AuthFailedException(InvalidCredentials);

        if (await userManager.IsLockedOutAsync(user))
            throw new AuthFailedException("Too many failed attempts. Please try again in a few minutes.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw new AuthFailedException(InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = JwtTokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new AuthFailedException("Invalid session. Please sign in again.");

        if (stored.RevokedAt is not null)
        {
            // Token reuse ⇒ the token may be stolen. Revoke every active session of this user.
            logger.LogWarning("Refresh token reuse detected for user {UserId} — revoking all sessions", stored.UserId);
            var active = await db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var token in active)
                token.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            throw new AuthFailedException("Invalid session. Please sign in again.");
        }

        if (stored.ExpiresAt < DateTime.UtcNow)
            throw new AuthFailedException("Session expired. Please sign in again.");

        var user = await userManager.FindByIdAsync(stored.UserId.ToString())
            ?? throw new AuthFailedException("Invalid session. Please sign in again.");

        // Rotate: revoke the old token and issue a fresh pair.
        var newToken = JwtTokenService.GenerateRefreshToken();
        var newHash = JwtTokenService.HashRefreshToken(newToken);
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByTokenHash = newHash;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow + jwt.RefreshTokenLifetime
        });
        await db.SaveChangesAsync(ct);

        var access = jwt.CreateAccessToken(user, await GetRolesAsync(user));
        return new AuthResponse(access.Token, access.ExpiresAt, newToken, await BuildUserDtoAsync(user, ct));
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = JwtTokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct);
        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null)
            return; // No account enumeration — the endpoint always answers 200.

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = $"{appOptions.Value.PublicBaseUrl.TrimEnd('/')}/reset-password" +
                   $"?email={WebUtility.UrlEncode(user.Email)}&token={WebUtility.UrlEncode(token)}";

        await emailSender.SendAsync(
            user.Email!,
            "Reset your WhatsOrder password",
            $"Hello {user.DisplayName},\n\nReset your password using this link:\n{link}\n\n" +
            "If you did not request this, you can safely ignore this email.",
            ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        const string invalidMessage = "This reset link is invalid or has expired.";

        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant())
            ?? throw new BusinessRuleException("invalid_reset_token", invalidMessage);

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var isTokenError = result.Errors.Any(e => e.Code == "InvalidToken");
            throw new BusinessRuleException(
                isTokenError ? "invalid_reset_token" : "identity",
                isTokenError ? invalidMessage : string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        // Force re-login everywhere after a password reset.
        var active = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in active)
            token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new AuthFailedException("Not authenticated.");
        return await BuildUserDtoAsync(user, ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
    {
        var refreshToken = JwtTokenService.GenerateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = JwtTokenService.HashRefreshToken(refreshToken),
            ExpiresAt = DateTime.UtcNow + jwt.RefreshTokenLifetime
        });
        await db.SaveChangesAsync(ct);

        var access = jwt.CreateAccessToken(user, await GetRolesAsync(user));
        return new AuthResponse(access.Token, access.ExpiresAt, refreshToken, await BuildUserDtoAsync(user, ct));
    }

    /// <summary>Accounts created before roles were emitted in tokens are all store owners.</summary>
    private async Task<IReadOnlyList<string>> GetRolesAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles.Count > 0 ? roles.ToList() : ["Owner"];
    }

    private async Task<UserDto> BuildUserDtoAsync(ApplicationUser user, CancellationToken ct)
    {
        var slug = await db.Stores
            .Where(s => s.OwnerId == user.Id)
            .Select(s => s.Slug)
            .FirstOrDefaultAsync(ct);

        var avatar = await db.BuyerProfiles
            .Where(p => p.UserId == user.Id)
            .Select(p => p.AvatarPath)
            .FirstOrDefaultAsync(ct);

        return new UserDto(user.Id, user.Email ?? "", user.DisplayName, slug is not null, slug,
            await GetRolesAsync(user), avatar);
    }
}
