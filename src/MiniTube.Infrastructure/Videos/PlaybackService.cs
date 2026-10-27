using Microsoft.EntityFrameworkCore;
using MiniTube.Application.Common;
using MiniTube.Application.Playback;
using MiniTube.Application.Videos;
using MiniTube.Domain.Entities;
using MiniTube.Domain.Enums;
using MiniTube.Infrastructure.Persistence;

namespace MiniTube.Infrastructure.Videos;

public class PlaybackService : IPlaybackService
{
    private const double CompletedRatio = 0.95;
    private static readonly TimeSpan ViewWindow = TimeSpan.FromHours(24);

    private sealed record ViewableVideo(Guid OwnerId, VideoVisibility Visibility, VideoStatus Status, double DurationSeconds);

    private readonly AppDbContext _db;

    public PlaybackService(AppDbContext db) => _db = db;

    // ---------------- views ----------------

    public async Task<ViewResultDto> RegisterViewAsync(Guid videoId, Guid? userId, string viewerKey, CancellationToken ct = default)
    {
        await GetViewableAsync(videoId, userId, ct);

        var trackedUser = await ResolveTrackedUserAsync(userId, ct);
        var since = DateTime.UtcNow - ViewWindow;

        var counted = await InTransactionAsync(async () =>
        {
            bool already;
            if (trackedUser is Guid uid)
            {
                already = await _db.VideoViews.AnyAsync(
                    x => x.VideoId == videoId && x.UserId == uid && x.CreatedAt > since, ct);
            }
            else
            {
                already = await _db.VideoViews.AnyAsync(
                    x => x.VideoId == videoId && x.UserId == null && x.ViewerKey == viewerKey && x.CreatedAt > since, ct);
            }

            if (already) return false;

            _db.VideoViews.Add(new VideoView
            {
                VideoId = videoId,
                UserId = trackedUser,
                ViewerKey = trackedUser is null ? viewerKey : null
            });
            await _db.SaveChangesAsync(ct);

            await _db.Videos.Where(v => v.Id == videoId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.ViewCount, v => v.ViewCount + 1), ct);
            return true;
        }, ct);

        var total = await _db.Videos.AsNoTracking().Where(v => v.Id == videoId).Select(v => v.ViewCount).SingleAsync(ct);
        return new ViewResultDto { Counted = counted, ViewCount = total };
    }

    // ---------------- progress / history ----------------

    public async Task UpdateProgressAsync(Guid userId, Guid videoId, double positionSeconds, CancellationToken ct = default)
    {
        var video = await GetViewableAsync(videoId, userId, ct);
        if (await ResolveTrackedUserAsync(userId, ct) is null) return; // history paused

        var position = Math.Max(0, positionSeconds);
        if (video.DurationSeconds > 0) position = Math.Min(position, video.DurationSeconds);
        var completed = video.DurationSeconds > 0 && position >= video.DurationSeconds * CompletedRatio;
        var now = DateTime.UtcNow;

        async Task<int> UpdateAsync() => await _db.WatchHistory
            .Where(h => h.UserId == userId && h.VideoId == videoId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.LastPositionSeconds, position)
                .SetProperty(h => h.Completed, completed)
                .SetProperty(h => h.LastWatchedAt, now), ct);

        if (await UpdateAsync() > 0) return;

        try
        {
            _db.WatchHistory.Add(new WatchHistoryEntry
            {
                UserId = userId,
                VideoId = videoId,
                LastPositionSeconds = position,
                Completed = completed,
                LastWatchedAt = now
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two heartbeats raced to create the row: the other one won, so just update it
            _db.ChangeTracker.Clear();
            await UpdateAsync();
        }
    }

    public async Task<PagedResult<WatchHistoryItemDto>> GetHistoryAsync(Guid userId, PagingQuery query, CancellationToken ct = default)
    {
        var (page, size) = Normalize(query);

        var q = _db.WatchHistory.AsNoTracking()
            .Where(h => h.UserId == userId
                        && h.Video.Status == VideoStatus.Ready
                        && (h.Video.Visibility != VideoVisibility.Private || h.Video.OwnerId == userId));

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(h => h.LastWatchedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(h => new { h.VideoId, h.LastPositionSeconds, h.Completed, h.LastWatchedAt })
            .ToListAsync(ct);

        var videos = await LoadListItemsAsync(rows.Select(r => r.VideoId).ToList(), ct);

        var items = rows
            .Where(r => videos.ContainsKey(r.VideoId))
            .Select(r => new WatchHistoryItemDto
            {
                Video = videos[r.VideoId],
                PositionSeconds = r.Completed ? 0 : r.LastPositionSeconds,
                Completed = r.Completed,
                WatchedAt = r.LastWatchedAt
            })
            .ToList();

        return new PagedResult<WatchHistoryItemDto> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    public Task RemoveFromHistoryAsync(Guid userId, Guid videoId, CancellationToken ct = default) =>
        _db.WatchHistory.Where(h => h.UserId == userId && h.VideoId == videoId).ExecuteDeleteAsync(ct);

    public Task ClearHistoryAsync(Guid userId, CancellationToken ct = default) =>
        _db.WatchHistory.Where(h => h.UserId == userId).ExecuteDeleteAsync(ct);

    // ---------------- reactions ----------------

    public async Task<ReactionStateDto> SetReactionAsync(Guid userId, Guid videoId, ReactionType? type, CancellationToken ct = default)
    {
        await GetViewableAsync(videoId, userId, ct);

        try
        {
            await InTransactionAsync(async () =>
            {
                var existing = await _db.VideoReactions.SingleOrDefaultAsync(r => r.UserId == userId && r.VideoId == videoId, ct);
                var old = existing?.Type;
                if (old == type) return 0; // nothing to do

                var likeDelta = (type == ReactionType.Like ? 1 : 0) - (old == ReactionType.Like ? 1 : 0);
                var dislikeDelta = (type == ReactionType.Dislike ? 1 : 0) - (old == ReactionType.Dislike ? 1 : 0);

                if (type is null)
                {
                    _db.VideoReactions.Remove(existing!);
                }
                else if (existing is null)
                {
                    _db.VideoReactions.Add(new VideoReaction { UserId = userId, VideoId = videoId, Type = type.Value });
                }
                else
                {
                    existing.Type = type.Value;
                    existing.CreatedAt = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync(ct);

                await _db.Videos.Where(v => v.Id == videoId).ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.LikeCount, v => v.LikeCount + likeDelta)
                    .SetProperty(v => v.DislikeCount, v => v.DislikeCount + dislikeDelta), ct);
                return 0;
            }, ct);
        }
        catch (DbUpdateException)
        {
            // Unique index (user, video) hit by two simultaneous requests
            throw AppException.Conflict("Your reaction is being saved. Please try again.", "reaction_conflict");
        }

        var likes = await _db.Videos.AsNoTracking().Where(v => v.Id == videoId).Select(v => v.LikeCount).SingleAsync(ct);
        var mine = await _db.VideoReactions.AsNoTracking()
            .Where(r => r.UserId == userId && r.VideoId == videoId)
            .Select(r => (ReactionType?)r.Type)
            .SingleOrDefaultAsync(ct);

        return new ReactionStateDto { MyReaction = mine, LikeCount = likes };
    }

    public async Task<PagedResult<VideoListItemDto>> ListLikedAsync(Guid userId, PagingQuery query, CancellationToken ct = default)
    {
        var (page, size) = Normalize(query);

        var q = _db.VideoReactions.AsNoTracking()
            .Where(r => r.UserId == userId
                        && r.Type == ReactionType.Like
                        && r.Video.Status == VideoStatus.Ready
                        && (r.Video.Visibility != VideoVisibility.Private || r.Video.OwnerId == userId));

        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(r => r.VideoId)
            .ToListAsync(ct);

        var videos = await LoadListItemsAsync(ids, ct);

        return new PagedResult<VideoListItemDto>
        {
            Items = ids.Where(videos.ContainsKey).Select(id => videos[id]).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    // ---------------- helpers ----------------

    /// <summary>Playback signals are only accepted for videos the caller is allowed to watch.</summary>
    private async Task<ViewableVideo> GetViewableAsync(Guid videoId, Guid? userId, CancellationToken ct)
    {
        var v = await _db.Videos.AsNoTracking()
            .Where(x => x.Id == videoId)
            .Select(x => new ViewableVideo(x.OwnerId, x.Visibility, x.Status, x.DurationSeconds))
            .SingleOrDefaultAsync(ct);

        if (v is null || v.Status != VideoStatus.Ready ||
            (v.Visibility == VideoVisibility.Private && v.OwnerId != userId))
            throw AppException.NotFound("Video not found");

        return v;
    }

    /// <summary>The user id when it may be recorded; null for anonymous viewers and users with paused history.</summary>
    private async Task<Guid?> ResolveTrackedUserAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is not Guid id) return null;

        var paused = await _db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => u.WatchHistoryPaused)
            .SingleOrDefaultAsync(ct);

        return paused ? null : id;
    }

    private async Task<Dictionary<Guid, VideoListItemDto>> LoadListItemsAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        var items = await _db.Videos.AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(VideoProjections.ListItem)
            .ToListAsync(ct);

        return items.ToDictionary(i => i.Id);
    }

    private static (int Page, int Size) Normalize(PagingQuery q) =>
        (Math.Max(1, q.Page), Math.Clamp(q.PageSize, 1, 100));

    /// <summary>Transaction that is safe with SqlServer's retrying execution strategy.</summary>
    private Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear(); // a retry must not see entities from the failed attempt
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var result = await work();
            await tx.CommitAsync(ct);
            return result;
        });
    }
}
