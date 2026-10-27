using MiniTube.Application.Common;

namespace MiniTube.Application.Videos;

public interface IVideoService
{
    Task<VideoDto> CreateAsync(Guid ownerId, CreateVideoRequest request, CancellationToken ct = default);

    /// <summary>Streams the raw file to disk, then queues it for processing.</summary>
    Task<VideoDto> UploadSourceAsync(Guid ownerId, Guid videoId, string? fileName, long? contentLength, Stream body, CancellationToken ct = default);

    Task<VideoDto> GetAsync(Guid videoId, Guid? requesterId, bool isAdmin, CancellationToken ct = default);
    Task<PagedResult<VideoListItemDto>> ListPublicAsync(VideoListQuery query, CancellationToken ct = default);
    Task<PagedResult<VideoListItemDto>> ListMineAsync(Guid ownerId, VideoListQuery query, CancellationToken ct = default);

    Task<VideoDto> UpdateAsync(Guid requesterId, bool isAdmin, Guid videoId, UpdateVideoRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid requesterId, bool isAdmin, Guid videoId, CancellationToken ct = default);

    /// <summary>Re-queues a Failed video (e.g. after fixing FFmpeg).</summary>
    Task<VideoDto> ReprocessAsync(Guid requesterId, bool isAdmin, Guid videoId, CancellationToken ct = default);
}
