using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniTube.Application.Auth;
using MiniTube.Application.Common;
using MiniTube.Domain.Constants;
using MiniTube.Domain.Entities;
using MiniTube.Infrastructure.Persistence;

namespace MiniTube.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;

    public AuthService(UserManager<ApplicationUser> users, AppDbContext db, ITokenService tokens)
    {
        _users = users;
        _db = db;
        _tokens = tokens;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default)
    {
        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        (await _users.CreateAsync(user, request.Password)).ThrowIfFailed("Registration failed");
        (await _users.AddToRoleAsync(user, Roles.User)).ThrowIfFailed();

        return await IssueTokensAsync(user, ip, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default)
    {
        var identifier = request.Identifier.Trim();
        var user = identifier.Contains('@')
            ? await _users.FindByEmailAsync(identifier)
            : await _users.FindByNameAsync(identifier);

        // Same message for "no such user" and "wrong password" to avoid user enumeration
        if (user is null)
            throw AppException.Unauthorized("Invalid credentials");

        if (await _users.IsLockedOutAsync(user))
            throw AppException.Forbidden("Account is locked. Try again later or contact support.");

        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            await _users.AccessFailedAsync(user);
            throw AppException.Unauthorized("Invalid credentials");
        }

        await _users.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, ip, ct);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = _tokens.Hash(refreshToken);
        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
            throw AppException.Unauthorized("Invalid refresh token");

        // A revoked token being presented again => likely stolen. Kill every session of that user.
        if (existing.RevokedAt is not null)
        {
            await RevokeAllAsync(existing.UserId, ip, ct);
            throw AppException.Unauthorized("Refresh token reuse detected. Please log in again.");
        }

        if (existing.IsExpired)
            throw AppException.Unauthorized("Refresh token expired");

        if (await _users.IsLockedOutAsync(existing.User))
            throw AppException.Forbidden("Account is locked.");

        var roles = await _users.GetRolesAsync(existing.User);
        var (access, accessExp) = _tokens.CreateAccessToken(existing.User, roles);

        // Rotate
        var newRaw = _tokens.GenerateRefreshToken();
        var newHash = _tokens.Hash(newRaw);
        var newExpiry = _tokens.GetRefreshTokenExpiry();

        existing.RevokedAt = DateTime.UtcNow;
        existing.RevokedByIp = ip;
        existing.ReplacedByTokenHash = newHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = newHash,
            ExpiresAt = newExpiry,
            CreatedByIp = ip
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResponse
        {
            AccessToken = access,
            AccessTokenExpiresAt = accessExp,
            RefreshToken = newRaw,
            RefreshTokenExpiresAt = newExpiry,
            User = existing.User.ToDto(roles)
        };
    }

    public async Task LogoutAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = _tokens.Hash(refreshToken);
        var token = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.RevokedAt is not null) return; // idempotent

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ip;
        await _db.SaveChangesAsync(ct);
    }

    public Task LogoutAllAsync(Guid userId, string? ip, CancellationToken ct = default) =>
        RevokeAllAsync(userId, ip, ct);

    // ---- helpers ----

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, string? ip, CancellationToken ct)
    {
        var roles = await _users.GetRolesAsync(user);
        var (access, accessExp) = _tokens.CreateAccessToken(user, roles);

        var raw = _tokens.GenerateRefreshToken();
        var expiry = _tokens.GetRefreshTokenExpiry();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.Hash(raw),
            ExpiresAt = expiry,
            CreatedByIp = ip
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResponse
        {
            AccessToken = access,
            AccessTokenExpiresAt = accessExp,
            RefreshToken = raw,
            RefreshTokenExpiresAt = expiry,
            User = user.ToDto(roles)
        };
    }

    private async Task RevokeAllAsync(Guid userId, string? ip, CancellationToken ct)
    {
        DateTime? now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedByIp, ip), ct);
    }
}
