using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniTube.Api.Extensions;
using MiniTube.Application.Users;

namespace MiniTube.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken ct) =>
        Ok(await _users.GetMeAsync(User.GetUserId(), ct));

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await _users.UpdateProfileAsync(User.GetUserId(), request, ct));

    [Authorize]
    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await _users.ChangePasswordAsync(User.GetUserId(), request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return NoContent();
    }

    /// <summary>Public profile (channel owner info). No auth needed.</summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicUserDto>> GetPublic(Guid id, CancellationToken ct) =>
        Ok(await _users.GetPublicAsync(id, ct));
}
