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

    /// <summary>multipart/form-data with a "file" field. JPEG/PNG/WebP, max 2 MB; stored as 256x256 WebP.</summary>
    [Authorize]
    [HttpPut("me/avatar")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UserDto>> SetAvatar(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await _users.SetAvatarAsync(User.GetUserId(), stream, file.Length, ct));
    }

    [Authorize]
    [HttpDelete("me/avatar")]
    public async Task<ActionResult<UserDto>> RemoveAvatar(CancellationToken ct) =>
        Ok(await _users.RemoveAvatarAsync(User.GetUserId(), ct));

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
