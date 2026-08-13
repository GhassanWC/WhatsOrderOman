namespace WhatsOrder.Application.Auth;

/// <summary>Implemented in Infrastructure on top of ASP.NET Core Identity.</summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Rotates the refresh token; reuse of a revoked token revokes the whole family.</summary>
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LogoutAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Always succeeds (no account enumeration). Sends a reset link via IEmailSender.</summary>
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);

    Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default);
}
