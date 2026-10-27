using MiniTube.Application.Common;
using MiniTube.Application.Videos;
using MiniTube.Domain.Enums;

namespace MiniTube.Application.Playback;

public interface IPlaybackService
{
    /// <param name="viewerKey">One-way hash of IP + user agent, used for anonymous viewers.</param>
    Task<ViewResultDto> RegisterViewAsync(Guid videoId, Guid? userId, string viewerKey, CancellationToken ct = default);

    /// <summary>Heartbeat from the player. No-op when the user paused their watch history.</summary>
    Task UpdateProgressAsync(Guid userId, Guid videoId, double positionSeconds, CancellationToken ct = default);

    Task<ReactionStateDto> SetReactionAsync(Guid userId, Guid videoId, ReactionType? type, CancellationToken ct = default);
    Task<PagedResult<VideoListItemDto>> ListLikedAsync(Guid userId, PagingQuery query, CancellationToken ct = default);

    Task<PagedResult<WatchHistoryItemDto>> GetHistoryAsync(Guid userId, PagingQuery query, CancellationToken ct = default);
    Task RemoveFromHistoryAsync(Guid userId, Guid videoId, CancellationToken ct = default);
    Task ClearHistoryAsync(Guid userId, CancellationToken ct = default);
}
