using MiniTube.Application.Common;
using MiniTube.Application.Videos;

namespace MiniTube.Application.Search;

public interface ISearchService
{
    Task<PagedResult<VideoListItemDto>> SearchVideosAsync(VideoSearchQuery query, CancellationToken ct = default);
    Task<PagedResult<ChannelSearchResultDto>> SearchChannelsAsync(ChannelSearchQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<SearchSuggestionDto>> SuggestAsync(string? q, CancellationToken ct = default);
}

public interface ISearchIndexer
{
    /// <summary>Fills the search columns of rows that have none yet (e.g. data created before search existed).</summary>
    Task<ReindexResultDto> BackfillMissingAsync(CancellationToken ct = default);

    /// <summary>Recomputes the search columns of EVERY video and user (after changing the normalisation rules).</summary>
    Task<ReindexResultDto> ReindexAllAsync(CancellationToken ct = default);
}
