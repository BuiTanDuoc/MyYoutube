using MiniTube.Application.Common;
using MiniTube.Application.Playback;
using MiniTube.Application.Videos;

namespace MiniTube.Application.Recommendations;

public interface IRecommendationService
{
    /// <summary>Sidebar / "up next" list for the watch page. Not personalised (so it can be cached).</summary>
    Task<IReadOnlyList<VideoListItemDto>> GetRelatedAsync(Guid videoId, Guid? requesterId, int limit, CancellationToken ct = default);

    /// <summary>Most viewed + liked recently. Never empty: quiet periods are filled with all-time popular videos.</summary>
    Task<PagedResult<VideoListItemDto>> GetTrendingAsync(TrendingWindow window, PagingQuery query, CancellationToken ct = default);

    /// <summary>Home feed. Personalised from likes + watch history; trending for anonymous / new users.</summary>
    Task<PagedResult<VideoListItemDto>> GetRecommendedAsync(Guid? userId, PagingQuery query, CancellationToken ct = default);

    /// <summary>Unfinished videos (watched at least 5 s), most recent first, with the position to resume at.</summary>
    Task<IReadOnlyList<WatchHistoryItemDto>> GetContinueWatchingAsync(Guid userId, int limit, CancellationToken ct = default);
}
