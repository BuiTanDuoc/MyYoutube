using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniTube.Api.Extensions;
using MiniTube.Application.Common;
using MiniTube.Application.Playback;
using MiniTube.Application.Users;

namespace MiniTube.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me/history")]
public class HistoryController : ControllerBase
{
    private readonly IPlaybackService _playback;
    private readonly IUserService _users;

    public HistoryController(IPlaybackService playback, IUserService users)
    {
        _playback = playback;
        _users = users;
    }

    /// <summary>Most recently watched first. Only videos the user can still watch are listed.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<WatchHistoryItemDto>>> Get([FromQuery] PagingQuery query, CancellationToken ct) =>
        Ok(await _playback.GetHistoryAsync(User.GetUserId(), query, ct));

    [HttpDelete("{videoId:guid}")]
    public async Task<IActionResult> Remove(Guid videoId, CancellationToken ct)
    {
        await _playback.RemoveFromHistoryAsync(User.GetUserId(), videoId, ct);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await _playback.ClearHistoryAsync(User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Pause / resume recording of the watch history. Returns the updated profile.</summary>
    [HttpPut("settings")]
    public async Task<ActionResult<UserDto>> SetPaused(SetWatchHistoryPausedRequest request, CancellationToken ct) =>
        Ok(await _users.SetWatchHistoryPausedAsync(User.GetUserId(), request.Paused, ct));
}
