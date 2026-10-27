using MiniTube.Application.Common;

namespace MiniTube.Application.Playlists;

public interface IPlaylistService
{
    Task<PlaylistDto> CreateAsync(Guid ownerId, CreatePlaylistRequest request, CancellationToken ct = default);
    Task<PlaylistDto> GetAsync(Guid playlistId, Guid? requesterId, bool isAdmin, CancellationToken ct = default);

    /// <summary>The user's built-in "Watch later" playlist (created on first use).</summary>
    Task<PlaylistDto> GetWatchLaterAsync(Guid userId, CancellationToken ct = default);

    Task<PagedResult<PlaylistDto>> ListMineAsync(Guid userId, PlaylistListQuery query, CancellationToken ct = default);
    Task<PagedResult<PlaylistDto>> ListPublicAsync(PlaylistListQuery query, CancellationToken ct = default);
    Task<PagedResult<PlaylistItemDto>> GetItemsAsync(Guid playlistId, Guid? requesterId, bool isAdmin, PagingQuery query, CancellationToken ct = default);

    Task<PlaylistDto> UpdateAsync(Guid userId, Guid playlistId, UpdatePlaylistRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid requesterId, bool isAdmin, Guid playlistId, CancellationToken ct = default);

    Task<PlaylistItemDto> AddItemAsync(Guid userId, Guid playlistId, Guid videoId, CancellationToken ct = default);
    Task RemoveItemAsync(Guid userId, Guid playlistId, Guid itemId, CancellationToken ct = default);
    Task RemoveVideoAsync(Guid userId, Guid playlistId, Guid videoId, CancellationToken ct = default);
    Task MoveItemAsync(Guid userId, Guid playlistId, Guid itemId, Guid? afterItemId, CancellationToken ct = default);
}
