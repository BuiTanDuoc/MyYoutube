using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MiniTube.Application.Common;
using MiniTube.Application.Playlists;
using MiniTube.Application.Videos;
using MiniTube.Domain.Entities;
using MiniTube.Domain.Enums;
using MiniTube.Infrastructure.Persistence;
using MiniTube.Infrastructure.Videos;

namespace MiniTube.Infrastructure.Playlists;

public class PlaylistService : IPlaylistService
{
    private const int MaxCustomPlaylistsPerUser = 100;
    private const int MaxItemsPerPlaylist = 5000;

    private sealed record PlaylistMeta(Guid OwnerId, VideoVisibility Visibility, PlaylistKind Kind);

    private readonly AppDbContext _db;

    public PlaylistService(AppDbContext db) => _db = db;

    // ---------------- create / read ----------------

    public async Task<PlaylistDto> CreateAsync(Guid ownerId, CreatePlaylistRequest request, CancellationToken ct = default)
    {
        var count = await _db.Playlists.CountAsync(p => p.OwnerId == ownerId && p.Kind == PlaylistKind.Custom, ct);
        if (count >= MaxCustomPlaylistsPerUser)
            throw AppException.Conflict($"You can have at most {MaxCustomPlaylistsPerUser} playlists.", "playlist_limit");

        var playlist = new Playlist
        {
            OwnerId = ownerId,
            Title = request.Title.Trim(),
            Description = Clean(request.Description),
            Visibility = request.Visibility,
            Kind = PlaylistKind.Custom
        };
        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync(ct);

        return await LoadDtoAsync(playlist.Id, ownerId, ct);
    }

    public async Task<PlaylistDto> GetAsync(Guid playlistId, Guid? requesterId, bool isAdmin, CancellationToken ct = default)
    {
        var meta = await GetMetaAsync(playlistId, ct);
        if (!CanView(meta, requesterId, isAdmin)) throw AppException.NotFound("Playlist not found");

        return await LoadDtoAsync(playlistId, requesterId ?? Guid.Empty, ct);
    }

    public async Task<PlaylistDto> GetWatchLaterAsync(Guid userId, CancellationToken ct = default)
    {
        var id = await EnsureWatchLaterAsync(userId, ct);
        return await LoadDtoAsync(id, userId, ct);
    }

    public async Task<PagedResult<PlaylistDto>> ListMineAsync(Guid userId, PlaylistListQuery query, CancellationToken ct = default)
    {
        await EnsureWatchLaterAsync(userId, ct); // the "Save to..." dialog always offers it

        var q = _db.Playlists.AsNoTracking()
            .Where(p => p.OwnerId == userId)
            .OrderBy(p => p.Kind == PlaylistKind.WatchLater ? 0 : 1)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt);

        return await PageAsync(q, query, userId, ct);
    }

    public Task<PagedResult<PlaylistDto>> ListPublicAsync(PlaylistListQuery query, CancellationToken ct = default)
    {
        // Public custom playlists that have at least one video an anonymous visitor can see
        var q = _db.Playlists.AsNoTracking()
            .Where(p => p.Visibility == VideoVisibility.Public
                        && p.Kind == PlaylistKind.Custom
                        && p.Items.Any(i => i.Video.Status == VideoStatus.Ready && i.Video.Visibility != VideoVisibility.Private));

        if (query.OwnerId is Guid owner) q = q.Where(p => p.OwnerId == owner);

        return PageAsync(q.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt), query, Guid.Empty, ct);
    }

    public async Task<PagedResult<PlaylistItemDto>> GetItemsAsync(
        Guid playlistId, Guid? requesterId, bool isAdmin, PagingQuery query, CancellationToken ct = default)
    {
        var meta = await GetMetaAsync(playlistId, ct);
        if (!CanView(meta, requesterId, isAdmin)) throw AppException.NotFound("Playlist not found");

        var viewerId = requesterId ?? Guid.Empty;
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var q = _db.PlaylistItems.AsNoTracking()
            .Where(i => i.PlaylistId == playlistId
                        && i.Video.Status == VideoStatus.Ready
                        && (i.Video.Visibility != VideoVisibility.Private || i.Video.OwnerId == viewerId));

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(i => i.Position).ThenBy(i => i.AddedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(i => new { i.Id, i.Position, i.AddedAt, i.VideoId })
            .ToListAsync(ct);

        var ids = rows.Select(r => r.VideoId).ToList();
        var videos = ids.Count == 0
            ? new Dictionary<Guid, VideoListItemDto>()
            : (await _db.Videos.AsNoTracking().Where(v => ids.Contains(v.Id)).Select(VideoProjections.ListItem).ToListAsync(ct))
                .ToDictionary(v => v.Id);

        return new PagedResult<PlaylistItemDto>
        {
            Items = rows.Where(r => videos.ContainsKey(r.VideoId))
                .Select(r => new PlaylistItemDto { ItemId = r.Id, Position = r.Position, AddedAt = r.AddedAt, Video = videos[r.VideoId] })
                .ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    // ---------------- update / delete ----------------

    public async Task<PlaylistDto> UpdateAsync(Guid userId, Guid playlistId, UpdatePlaylistRequest request, CancellationToken ct = default)
    {
        var meta = await GetMetaAsync(playlistId, ct);
        EnsureOwner(meta, userId);

        if (meta.Kind == PlaylistKind.WatchLater)
            throw AppException.Conflict("The Watch later playlist cannot be edited.", "system_playlist");

        var title = request.Title.Trim();
        var description = Clean(request.Description);
        DateTime? now = DateTime.UtcNow;

        await _db.Playlists.Where(p => p.Id == playlistId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Title, title)
            .SetProperty(p => p.Description, description)
            .SetProperty(p => p.Visibility, request.Visibility)
            .SetProperty(p => p.UpdatedAt, now), ct);

        return await LoadDtoAsync(playlistId, userId, ct);
    }

    public async Task DeleteAsync(Guid requesterId, bool isAdmin, Guid playlistId, CancellationToken ct = default)
    {
        var meta = await GetMetaAsync(playlistId, ct);

        if (!(isAdmin || meta.OwnerId == requesterId))
            throw meta.Visibility != VideoVisibility.Private
                ? AppException.Forbidden("You can only delete your own playlists.")
                : AppException.NotFound("Playlist not found");

        if (meta.Kind == PlaylistKind.WatchLater)
            throw AppException.Conflict("The Watch later playlist cannot be deleted.", "system_playlist");

        await _db.Playlists.Where(p => p.Id == playlistId).ExecuteDeleteAsync(ct); // items cascade
    }

    // ---------------- items ----------------

    public async Task<PlaylistItemDto> AddItemAsync(Guid userId, Guid playlistId, Guid videoId, CancellationToken ct = default)
    {
        var meta = await GetMetaAsync(playlistId, ct);
        EnsureOwner(meta, userId);

        // You can only save videos you are allowed to watch
        var video = await _db.Videos.AsNoTracking()
            .Where(v => v.Id == videoId)
            .Select(v => new { v.OwnerId, v.Visibility, v.Status })
            .SingleOrDefaultAsync(ct);

        if (video is null || video.Status != VideoStatus.Ready ||
            (video.Visibility == VideoVisibility.Private && video.OwnerId != userId))
            throw AppException.NotFound("Video not found");

        var itemId = await InTransactionAsync(async () =>
        {
            var existing = await _db.PlaylistItems.AsNoTracking()
                .Where(i => i.PlaylistId == playlistId && i.VideoId == videoId)
                .Select(i => (Guid?)i.Id)
                .SingleOrDefaultAsync(ct);
            if (existing is Guid existingId) return existingId; // already there: idempotent

            var count = await _db.PlaylistItems.CountAsync(i => i.PlaylistId == playlistId, ct);
            if (count >= MaxItemsPerPlaylist)
                throw AppException.Conflict($"A playlist can hold at most {MaxItemsPerPlaylist} videos.", "playlist_full");

            var max = await _db.PlaylistItems.Where(i => i.PlaylistId == playlistId).MaxAsync(i => (int?)i.Position, ct);
            var item = new PlaylistItem { PlaylistId = playlistId, VideoId = videoId, Position = (max ?? -1) + 1 };
            _db.PlaylistItems.Add(item);
            await _db.SaveChangesAsync(ct);
            await TouchAsync(playlistId, ct);
            return item.Id;
        }, ct);

        var row = await _db.PlaylistItems.AsNoTracking().Where(i => i.Id == itemId)
            .Select(i => new { i.Id, i.Position, i.AddedAt }).SingleAsync(ct);
        var listItem = await _db.Videos.AsNoTracking().Where(v => v.Id == videoId).Select(VideoProjections.ListItem).SingleAsync(ct);

        return new PlaylistItemDto { ItemId = row.Id, Position = row.Position, AddedAt = row.AddedAt, Video = listItem };
    }

    public async Task RemoveItemAsync(Guid userId, Guid playlistId, Guid itemId, CancellationToken ct = default)
    {
        EnsureOwner(await GetMetaAsync(playlistId, ct), userId);

        var removed = await _db.PlaylistItems.Where(i => i.Id == itemId && i.PlaylistId == playlistId).ExecuteDeleteAsync(ct);
        if (removed > 0) await TouchAsync(playlistId, ct); // no renumbering needed: gaps are fine
    }

    public async Task RemoveVideoAsync(Guid userId, Guid playlistId, Guid videoId, CancellationToken ct = default)
    {
        EnsureOwner(await GetMetaAsync(playlistId, ct), userId);

        var removed = await _db.PlaylistItems.Where(i => i.PlaylistId == playlistId && i.VideoId == videoId).ExecuteDeleteAsync(ct);
        if (removed > 0) await TouchAsync(playlistId, ct);
    }

    public async Task MoveItemAsync(Guid userId, Guid playlistId, Guid itemId, Guid? afterItemId, CancellationToken ct = default)
    {
        EnsureOwner(await GetMetaAsync(playlistId, ct), userId);

        await InTransactionAsync(async () =>
        {
            var current = await _db.PlaylistItems.AsNoTracking()
                .Where(i => i.Id == itemId && i.PlaylistId == playlistId)
                .Select(i => (int?)i.Position)
                .SingleOrDefaultAsync(ct)
                ?? throw AppException.NotFound("Item not found");

            int newPosition;
            if (afterItemId is null)
            {
                newPosition = 0;
            }
            else
            {
                if (afterItemId == itemId) return 0;

                var after = await _db.PlaylistItems.AsNoTracking()
                    .Where(i => i.Id == afterItemId && i.PlaylistId == playlistId)
                    .Select(i => (int?)i.Position)
                    .SingleOrDefaultAsync(ct)
                    ?? throw AppException.BadRequest("afterItemId does not belong to this playlist.");

                // Once the moved item leaves its old slot, items after it shift down by one
                newPosition = after < current ? after + 1 : after;
            }

            if (newPosition == current) return 0;

            if (newPosition < current)
            {
                await _db.PlaylistItems
                    .Where(i => i.PlaylistId == playlistId && i.Position >= newPosition && i.Position < current)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Position, i => i.Position + 1), ct);
            }
            else
            {
                await _db.PlaylistItems
                    .Where(i => i.PlaylistId == playlistId && i.Position > current && i.Position <= newPosition)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Position, i => i.Position - 1), ct);
            }

            await _db.PlaylistItems.Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Position, newPosition), ct);
            await TouchAsync(playlistId, ct);
            return 0;
        }, ct);
    }

    // ---------------- helpers ----------------

    /// <summary>EF projection; the item count/thumbnail only consider videos the viewer may see.</summary>
    private static Expression<Func<Playlist, PlaylistDto>> Project(Guid viewerId) => p => new PlaylistDto
    {
        Id = p.Id,
        Title = p.Title,
        Description = p.Description,
        Visibility = p.Visibility,
        Kind = p.Kind,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
        ItemCount = p.Items.Count(i => i.Video.Status == VideoStatus.Ready
                                       && (i.Video.Visibility != VideoVisibility.Private || i.Video.OwnerId == viewerId)),
        ThumbnailUrl = p.Items
            .Where(i => i.Video.Status == VideoStatus.Ready
                        && (i.Video.Visibility != VideoVisibility.Private || i.Video.OwnerId == viewerId))
            .OrderBy(i => i.Position)
            .Select(i => i.Video.ThumbnailUrl)
            .FirstOrDefault(),
        Owner = new VideoOwnerDto
        {
            Id = p.Owner.Id,
            UserName = p.Owner.UserName ?? string.Empty,
            DisplayName = p.Owner.DisplayName,
            AvatarUrl = p.Owner.AvatarUrl
        }
    };

    private Task<PlaylistDto> LoadDtoAsync(Guid playlistId, Guid viewerId, CancellationToken ct) =>
        _db.Playlists.AsNoTracking().Where(p => p.Id == playlistId).Select(Project(viewerId)).SingleAsync(ct);

    private async Task<PagedResult<PlaylistDto>> PageAsync(
        IQueryable<Playlist> ordered, PlaylistListQuery query, Guid viewerId, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var total = await ordered.CountAsync(ct);
        var items = await ordered.Skip((page - 1) * size).Take(size).Select(Project(viewerId)).ToListAsync(ct);

        if (query.VideoId is Guid videoId && items.Count > 0)
        {
            var ids = items.Select(i => i.Id).ToList();
            var containing = (await _db.PlaylistItems.AsNoTracking()
                .Where(i => ids.Contains(i.PlaylistId) && i.VideoId == videoId)
                .Select(i => i.PlaylistId)
                .ToListAsync(ct)).ToHashSet();

            foreach (var item in items) item.ContainsVideo = containing.Contains(item.Id);
        }

        return new PagedResult<PlaylistDto> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    private async Task<PlaylistMeta> GetMetaAsync(Guid playlistId, CancellationToken ct) =>
        await _db.Playlists.AsNoTracking()
            .Where(p => p.Id == playlistId)
            .Select(p => new PlaylistMeta(p.OwnerId, p.Visibility, p.Kind))
            .SingleOrDefaultAsync(ct)
        ?? throw AppException.NotFound("Playlist not found");

    private static bool CanView(PlaylistMeta meta, Guid? requesterId, bool isAdmin) =>
        meta.Visibility != VideoVisibility.Private || isAdmin || (requesterId is not null && requesterId == meta.OwnerId);

    private static void EnsureOwner(PlaylistMeta meta, Guid userId)
    {
        if (meta.OwnerId == userId) return;
        throw meta.Visibility != VideoVisibility.Private
            ? AppException.Forbidden("You can only change your own playlists.")
            : AppException.NotFound("Playlist not found");
    }

    private async Task<Guid> EnsureWatchLaterAsync(Guid userId, CancellationToken ct)
    {
        var existing = await _db.Playlists.AsNoTracking()
            .Where(p => p.OwnerId == userId && p.Kind == PlaylistKind.WatchLater)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(ct);
        if (existing is Guid id) return id;

        var playlist = new Playlist
        {
            OwnerId = userId,
            Title = "Watch later",
            Visibility = VideoVisibility.Private,
            Kind = PlaylistKind.WatchLater
        };

        try
        {
            _db.Playlists.Add(playlist);
            await _db.SaveChangesAsync(ct);
            return playlist.Id;
        }
        catch (DbUpdateException)
        {
            // Another request created it first (filtered unique index): use that one
            _db.ChangeTracker.Clear();
            return await _db.Playlists.AsNoTracking()
                .Where(p => p.OwnerId == userId && p.Kind == PlaylistKind.WatchLater)
                .Select(p => p.Id)
                .SingleAsync(ct);
        }
    }

    private Task TouchAsync(Guid playlistId, CancellationToken ct)
    {
        DateTime? now = DateTime.UtcNow;
        return _db.Playlists.Where(p => p.Id == playlistId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAt, now), ct);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Transaction that is safe with SqlServer's retrying execution strategy.</summary>
    private Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var result = await work();
            await tx.CommitAsync(ct);
            return result;
        });
    }
}
