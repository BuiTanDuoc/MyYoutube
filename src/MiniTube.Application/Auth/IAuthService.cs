namespace MiniTube.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, string? ip, CancellationToken ct = default);
    Task LogoutAllAsync(Guid userId, string? ip, CancellationToken ct = default);
}
