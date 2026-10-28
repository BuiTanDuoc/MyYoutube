using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniTube.Api.Extensions;
using MiniTube.Application.Common;
using MiniTube.Application.Playback;
using MiniTube.Application.Recommendations;
using MiniTube.Application.Videos;

namespace MiniTube.Api.Controllers;

/// <summary>Related videos, trending, the home feed and "continue watching".</summary>
[ApiController]
[Route("api")]
[EnableRateLimiting("search")] // same per-user / per-IP budget as search
public class FeedController : ControllerBase
{
    private readonly IRecommendationService _recommendations;

    public FeedController(IRecommendationService recommendations) => _recommendations = recommendations;

    /// <summary>Sidebar for the watch page. The first item doubles as "up next".</summary>
    [AllowAnonymous]
    [HttpGet("videos/{id:guid}/related")]
    public async Task<ActionResult<IReadOnlyList<VideoListItemDto>>> Related(Guid id, [FromQuery] int limit = 20, CancellationToken ct = default) =>
        Ok(await _recommendations.GetRelatedAsync(id, User.TryGetUserId(), limit, ct));

    [AllowAnonymous]
    [HttpGet("feed/trending")]
    public async Task<ActionResult<PagedResult<VideoListItemDto>>> Trending(
        [FromQuery] TrendingWindow window = TrendingWindow.Week, [FromQuery] PagingQuery? query = null, CancellationToken ct = default) =>
        Ok(await _recommendations.GetTrendingAsync(window, query ?? new PagingQuery(), ct));

    /// <summary>Home feed. Send the Bearer token to get a personalised feed; anonymous visitors get trending.</summary>
    [AllowAnonymous]
    [HttpGet("feed/recommended")]
    public async Task<ActionResult<PagedResult<VideoListItemDto>>> Recommended([FromQuery] PagingQuery? query = null, CancellationToken ct = default) =>
        Ok(await _recommendations.GetRecommendedAsync(User.TryGetUserId(), query ?? new PagingQuery(), ct));

    [Authorize]
    [HttpGet("feed/continue-watching")]
    public async Task<ActionResult<IReadOnlyList<WatchHistoryItemDto>>> ContinueWatching([FromQuery] int limit = 20, CancellationToken ct = default) =>
        Ok(await _recommendations.GetContinueWatchingAsync(User.GetUserId(), limit, ct));
}
