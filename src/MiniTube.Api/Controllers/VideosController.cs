using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MiniTube.Api.Extensions;
using MiniTube.Application.Common;
using MiniTube.Application.Videos;
using MiniTube.Domain.Constants;
using MiniTube.Infrastructure.Options;

namespace MiniTube.Api.Controllers;

[ApiController]
[Route("api/videos")]
public class VideosController : ControllerBase
{
    private readonly IVideoService _videos;
    private readonly VideoOptions _options;

    public VideosController(IVideoService videos, IOptions<VideoOptions> options)
    {
        _videos = videos;
        _options = options.Value;
    }

    private bool IsAdmin => User.IsInRole(Roles.Admin);

    /// <summary>Step 1 of an upload: create the metadata. Requires a confirmed email.</summary>
    [Authorize(Policy = Policies.VerifiedEmail)]
    [HttpPost]
    public async Task<ActionResult<VideoDto>> Create(CreateVideoRequest request, CancellationToken ct)
    {
        var dto = await _videos.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// Step 2: send the raw file as the request body (not multipart), e.g.
    /// PUT /api/videos/{id}/source?fileName=holiday.mp4  with the file bytes.
    /// The body is streamed straight to disk, so large files do not use memory.
    /// </summary>
    [Authorize(Policy = Policies.VerifiedEmail)]
    [HttpPut("{id:guid}/source")]
    public async Task<ActionResult<VideoDto>> UploadSource(Guid id, [FromQuery] string? fileName, CancellationToken ct)
    {
        // Kestrel's default body limit is ~28 MB; lift it for this endpoint only.
        var feature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = _options.MaxUploadBytes + 1024 * 1024;

        var dto = await _videos.UploadSourceAsync(
            User.GetUserId(), id, fileName, Request.ContentLength, Request.Body, ct);

        return Ok(dto);
    }

    /// <summary>Works with or without a token: private videos are only visible to their owner (and admins).</summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VideoDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _videos.GetAsync(id, User.TryGetUserId(), IsAdmin, ct));

    /// <summary>Public, ready videos, newest first. Use ownerId for a channel page.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PagedResult<VideoListItemDto>>> List([FromQuery] VideoListQuery query, CancellationToken ct) =>
        Ok(await _videos.ListPublicAsync(query, ct));

    /// <summary>The current user's videos in every status (for the "Your videos" / studio page).</summary>
    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<VideoListItemDto>>> Mine([FromQuery] VideoListQuery query, CancellationToken ct) =>
        Ok(await _videos.ListMineAsync(User.GetUserId(), query, ct));

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<VideoDto>> Update(Guid id, UpdateVideoRequest request, CancellationToken ct) =>
        Ok(await _videos.UpdateAsync(User.GetUserId(), IsAdmin, id, request, ct));

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _videos.DeleteAsync(User.GetUserId(), IsAdmin, id, ct);
        return NoContent();
    }

    /// <summary>Re-queue a Failed video.</summary>
    [Authorize]
    [HttpPost("{id:guid}/reprocess")]
    public async Task<ActionResult<VideoDto>> Reprocess(Guid id, CancellationToken ct) =>
        Ok(await _videos.ReprocessAsync(User.GetUserId(), IsAdmin, id, ct));
}
