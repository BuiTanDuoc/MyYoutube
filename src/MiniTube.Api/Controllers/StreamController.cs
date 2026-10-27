using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniTube.Api.Extensions;
using MiniTube.Application.Videos;
using MiniTube.Domain.Constants;

namespace MiniTube.Api.Controllers;

/// <summary>
/// Serves the HLS files (master playlist, rendition playlists, segments) after a visibility check.
/// Public/unlisted: anyone. Private: the owner or an admin with a Bearer token
/// (hls.js can send it through xhrSetup).
/// </summary>
[ApiController]
[Route("api/stream")]
public class StreamController : ControllerBase
{
    private readonly IVideoStreamService _stream;

    public StreamController(IVideoStreamService stream) => _stream = stream;

    [AllowAnonymous]
    [HttpGet("{videoId:guid}/{**file}")]
    public async Task<IActionResult> Get(Guid videoId, string file, CancellationToken ct)
    {
        var result = await _stream.GetFileAsync(videoId, file, User.TryGetUserId(), User.IsInRole(Roles.Admin), ct);

        Response.Headers.CacheControl = result.IsPrivate ? "private, max-age=3600" : "public, max-age=3600";
        return PhysicalFile(result.PhysicalPath, result.ContentType, enableRangeProcessing: true);
    }
}
