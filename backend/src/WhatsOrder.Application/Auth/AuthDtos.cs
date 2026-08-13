namespace WhatsOrder.Application.Auth;

/// <summary>AccountType: "seller" (default) creates a store-owner account, "buyer" a marketplace buyer.</summary>
public sealed record RegisterRequest(string Email, string Password, string DisplayName, string? AccountType = null);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool HasStore,
    string? StoreSlug,
    IReadOnlyList<string> Roles,
    string? AvatarUrl);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    UserDto User);
