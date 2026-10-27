using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniTube.Application.Common;
using MiniTube.Application.Search;
using MiniTube.Application.Videos;
using MiniTube.Domain.Constants;

namespace MiniTube.Api.Controllers;

[ApiController]
[Route("api/search")]
[EnableRateLimiting("search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _search;
    private readonly ISearchIndexer _indexer;

    public SearchController(ISearchService search, ISearchIndexer indexer)
    {
        _search = search;
        _indexer = indexer;
    }

    /// <summary>
    /// GET /api/search/videos?q=huong dan&amp;sort=Relevance|Newest|MostViewed|MostLiked
    ///                       &amp;uploadDate=Any|Today|Week|Month|Year&amp;duration=Any|Short|Medium|Long&amp;ownerId=
    /// Accent-insensitive ("hoc lap trinh" finds "học lập trình"). Without q it simply browses with the filters.
    /// Relevance ranking covers the best 300 matches.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("videos")]
    public async Task<ActionResult<PagedResult<VideoListItemDto>>> Videos([FromQuery] VideoSearchQuery query, CancellationToken ct) =>
        Ok(await _search.SearchVideosAsync(query, ct));

    [AllowAnonymous]
    [HttpGet("channels")]
    public async Task<ActionResult<PagedResult<ChannelSearchResultDto>>> Channels([FromQuery] ChannelSearchQuery query, CancellationToken ct) =>
        Ok(await _search.SearchChannelsAsync(query, ct));

    /// <summary>Autocomplete for the search box (up to 6 titles + 2 channels). Debounce ~250 ms on the client.</summary>
    [AllowAnonymous]
    [HttpGet("suggestions")]
    public async Task<ActionResult<IReadOnlyList<SearchSuggestionDto>>> Suggestions([FromQuery] string? q, CancellationToken ct) =>
        Ok(await _search.SuggestAsync(q, ct));

    /// <summary>Rebuilds the search columns of all videos and users (run after changing the normalisation rules).</summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("/api/admin/search/reindex")]
    public async Task<ActionResult<ReindexResultDto>> Reindex(CancellationToken ct) =>
        Ok(await _indexer.ReindexAllAsync(ct));
}
