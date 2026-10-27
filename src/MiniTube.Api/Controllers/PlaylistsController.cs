using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniTube.Api.Extensions;
using MiniTube.Application.Common;
using MiniTube.Application.Playlists;
using MiniTube.Domain.Constants;

namespace MiniTube.Api.Controllers;

[ApiController]
[Route("api/playlists")]
public class PlaylistsController : ControllerBase
{
    private readonly IPlaylistService _playlists;

    public PlaylistsController(IPlaylistService playlists) => _playlists = playlists;

    private bool IsAdmin => User.IsInRole(Roles.Admin);

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<PlaylistDto>> Create(CreatePlaylistRequest request, CancellationToken ct)
    {
        var dto = await _playlists.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Public playlists (channel page: add ?ownerId=).</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PagedResult<PlaylistDto>>> ListPublic([FromQuery] PlaylistListQuery query, CancellationToken ct) =>
        Ok(await _playlists.ListPublicAsync(query, ct));

    /// <summary>My playlists, "Watch later" first. Add ?videoId= to get containsVideo for the "Save to..." dialog.</summary>
    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<PlaylistDto>>> Mine([FromQuery] PlaylistListQuery query, CancellationToken ct) =>
        Ok(await _playlists.ListMineAsync(User.GetUserId(), query, ct));

    [Authorize]
    [HttpGet("watch-later")]
    public async Task<ActionResult<PlaylistDto>> WatchLater(CancellationToken ct) =>
        Ok(await _playlists.GetWatchLaterAsync(User.GetUserId(), ct));

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlaylistDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _playlists.GetAsync(id, User.TryGetUserId(), IsAdmin, ct));

    [AllowAnonymous]
    [HttpGet("{id:guid}/items")]
    public async Task<ActionResult<PagedResult<PlaylistItemDto>>> Items(Guid id, [FromQuery] PagingQuery query, CancellationToken ct) =>
        Ok(await _playlists.GetItemsAsync(id, User.TryGetUserId(), IsAdmin, query, ct));

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlaylistDto>> Update(Guid id, UpdatePlaylistRequest request, CancellationToken ct) =>
        Ok(await _playlists.UpdateAsync(User.GetUserId(), id, request, ct));

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _playlists.DeleteAsync(User.GetUserId(), IsAdmin, id, ct);
        return NoContent();
    }

    /// <summary>Appends a video (no-op returning the existing item if it is already in the playlist).</summary>
    [Authorize]
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<PlaylistItemDto>> AddItem(Guid id, AddPlaylistItemRequest request, CancellationToken ct) =>
        Ok(await _playlists.AddItemAsync(User.GetUserId(), id, request.VideoId, ct));

    [Authorize]
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId, CancellationToken ct)
    {
        await _playlists.RemoveItemAsync(User.GetUserId(), id, itemId, ct);
        return NoContent();
    }

    /// <summary>Convenience for the "Save to playlist" toggle, where the client knows the video but not the item id.</summary>
    [Authorize]
    [HttpDelete("{id:guid}/videos/{videoId:guid}")]
    public async Task<IActionResult> RemoveVideo(Guid id, Guid videoId, CancellationToken ct)
    {
        await _playlists.RemoveVideoAsync(User.GetUserId(), id, videoId, ct);
        return NoContent();
    }

    /// <summary>Drag and drop: place the item right after <c>afterItemId</c> (null = top).</summary>
    [Authorize]
    [HttpPut("{id:guid}/items/{itemId:guid}/move")]
    public async Task<IActionResult> MoveItem(Guid id, Guid itemId, MovePlaylistItemRequest request, CancellationToken ct)
    {
        await _playlists.MoveItemAsync(User.GetUserId(), id, itemId, request.AfterItemId, ct);
        return NoContent();
    }
}
