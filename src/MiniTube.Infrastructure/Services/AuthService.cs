using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniTube.Application.Auth;
using MiniTube.Application.Common;
using MiniTube.Domain.Constants;
using MiniTube.Domain.Entities;
using MiniTube.Infrastructure.Options;
using MiniTube.Infrastructure.Persistence;

namespace MiniTube.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IAppEmailSender _email;
    private readonly FrontendOptions _frontend;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> users,
        AppDbContext db,
        ITokenService tokens,
        IAppEmailSender email,
        IOptions<FrontendOptions> frontend,
        ILogger<AuthService> logger)
    {
        _users = users;
        _db = db;
        _tokens = tokens;
        _email = email;
        _frontend = frontend.Value;
        _logger = logger;
    }

    // ---------------- Register / login / refresh / logout ----------------

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, string language, CancellationToken ct = default)
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

        // A mail problem must never make registration fail; the user can use "resend".
        await TrySendConfirmationAsync(user, language, ct);

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
            throw AppException.Forbidden("Account is locked. Try again later or contact support.", "account_locked");

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
            throw AppException.Forbidden("Account is locked.", "account_locked");

        // Roles / email_verified are re-read here, so a refresh picks up changes (e.g. just confirmed email).
        var roles = await _users.GetRolesAsync(existing.User);
        var (access, accessExp) = _tokens.CreateAccessToken(existing.User, roles);

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

    // ---------------- Email confirmation ----------------

    public async Task ConfirmEmailAsync(string userId, string token, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId)
                   ?? throw InvalidLink("invalid_token");

        if (user.EmailConfirmed) return; // idempotent: clicking the link twice is fine

        var result = await _users.ConfirmEmailAsync(user, DecodeToken(token));
        if (!result.Succeeded) throw InvalidLink("invalid_token");
    }

    public async Task ResendConfirmationAsync(string email, string language, CancellationToken ct = default)
    {
        // Always "succeeds" from the caller's point of view: no account enumeration.
        var user = await _users.FindByEmailAsync(email.Trim());
        if (user is null || user.EmailConfirmed) return;
        await TrySendConfirmationAsync(user, language, ct);
    }

    // ---------------- Forgot / reset password ----------------

    public async Task ForgotPasswordAsync(string email, string language, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(email.Trim());
        if (user is null) return; // no account enumeration

        try
        {
            var token = EncodeToken(await _users.GeneratePasswordResetTokenAsync(user));
            var link = $"{FrontendBase}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={token}";
            var (subject, body) = EmailTemplates.ResetPassword(NormalizeLang(language), user.DisplayName, link);
            await _email.SendAsync(user.Email!, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to user {UserId}", user.Id);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, string? ip, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim())
                   ?? throw InvalidLink("invalid_token");

        var result = await _users.ResetPasswordAsync(user, DecodeToken(request.Token), request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "InvalidToken")) throw InvalidLink("invalid_token");
            result.ThrowIfFailed("Could not reset password"); // password policy errors
        }

        await _users.ResetAccessFailedCountAsync(user);
        await RevokeAllAsync(user.Id, ip, ct); // every device must log in again
    }

    // ---------------- helpers ----------------

    private string FrontendBase => _frontend.BaseUrl.TrimEnd('/');

    private static string NormalizeLang(string? lang) =>
        string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";

    private static AppException InvalidLink(string code) =>
        AppException.BadRequest("The link is invalid or has expired.", code: code);

    // Identity tokens contain '+', '/', '=' -> make them URL-safe
    private static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string DecodeToken(string encoded)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded)); }
        catch (FormatException) { throw InvalidLink("invalid_token"); }
    }

    private async Task TrySendConfirmationAsync(ApplicationUser user, string language, CancellationToken ct)
    {
        try
        {
            var token = EncodeToken(await _users.GenerateEmailConfirmationTokenAsync(user));
            var link = $"{FrontendBase}/confirm-email?userId={user.Id}&token={token}";
            var (subject, body) = EmailTemplates.ConfirmEmail(NormalizeLang(language), user.DisplayName, link);
            await _email.SendAsync(user.Email!, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send confirmation email to user {UserId}", user.Id);
        }
    }

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
