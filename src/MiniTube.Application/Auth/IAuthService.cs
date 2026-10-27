namespace MiniTube.Application.Auth;

public interface IAuthService
{
    /// <param name="language">"vi" or "en" — language of the emails sent.</param>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, string language, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, string? ip, CancellationToken ct = default);
    Task LogoutAllAsync(Guid userId, string? ip, CancellationToken ct = default);

    Task ConfirmEmailAsync(string userId, string token, CancellationToken ct = default);
    Task ResendConfirmationAsync(string email, string language, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, string language, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, string? ip, CancellationToken ct = default);
}
