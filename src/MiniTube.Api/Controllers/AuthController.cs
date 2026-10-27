using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniTube.Api.Extensions;
using MiniTube.Application.Auth;

namespace MiniTube.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct) =>
        Ok(await _auth.RegisterAsync(request, Ip, Request.GetLanguage(), ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(request, Ip, ct));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken ct) =>
        Ok(await _auth.RefreshAsync(request.RefreshToken, Ip, ct));

    /// <summary>Revokes one refresh token (this device).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request.RefreshToken, Ip, ct);
        return NoContent();
    }

    /// <summary>Revokes every refresh token of the current user (all devices).</summary>
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await _auth.LogoutAllAsync(User.GetUserId(), Ip, ct);
        return NoContent();
    }

    /// <summary>Called by the React /confirm-email page with the userId + token from the email link.</summary>
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        await _auth.ConfirmEmailAsync(request.UserId, request.Token, ct);
        return NoContent();
    }

    /// <summary>Always 204, whether or not the email exists (no account enumeration).</summary>
    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("email")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request, CancellationToken ct)
    {
        await _auth.ResendConfirmationAsync(request.Email, Request.GetLanguage(), ct);
        return NoContent();
    }

    /// <summary>Always 204, whether or not the email exists (no account enumeration).</summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("email")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request.Email, Request.GetLanguage(), ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, Ip, ct);
        return NoContent();
    }
}
