using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MiniTube.Application.Common;
using MiniTube.Application.Playback;
using MiniTube.Application.Recommendations;
using MiniTube.Application.Search;
using MiniTube.Application.Videos;
using MiniTube.Domain.Entities;
using MiniTube.Domain.Enums;
using MiniTube.Infrastructure.Persistence;
using MiniTube.Infrastructure.Videos;

namespace MiniTube.Infrastructure.Recommendations;

/// <summary>
/// Rule-based recommendations (no ML): candidate generation from a few cheap signals, then a weighted score.
/// Ranked id lists are cached for a few minutes so paging is stable and the database is not hammered.
/// Only Public + Ready videos are ever recommended (never unlisted or private ones).
/// </summary>
public class RecommendationService : IRecommendationService
{
    private static readonly TimeSpan RelatedTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FeedTtl = TimeSpan.FromMinutes(5);
    private const int MaxRankedIds = 200;
    private const int MaxPerOwnerInTopResults = 3;

    private sealed record Candidate(Guid Id, Guid OwnerId, string SearchTitle, long ViewCount, int LikeCount, DateTime? PublishedAt);

    private static readonly Expression<Func<Video, Candidate>> ToCandidate = v =>
        new Candidate(v.Id, v.OwnerId, v.SearchTitle ?? string.Empty, v.ViewCount, v.LikeCount, v.PublishedAt);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public RecommendationService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    // ======================= related =======================

    public async Task<IReadOnlyList<VideoListItemDto>> GetRelatedAsync(
        Guid videoId, Guid? requesterId, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        var current = await _db.Videos.AsNoTracking()
            .Where(v => v.Id == videoId)
            .Select(v => new { v.OwnerId, v.Visibility, v.Status, v.SearchTitle })
            .SingleOrDefaultAsync(ct);

        if (current is null || current.Status != VideoStatus.Ready ||
            (current.Visibility == VideoVisibility.Private && current.OwnerId != requesterId))
            throw AppException.NotFound("Video not found");

        var ids = await _cache.GetOrCreateAsync($"related:{videoId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RelatedTtl;
            return RankRelatedAsync(videoId, current.OwnerId, current.SearchTitle ?? string.Empty, ct);
        }) ?? [];

        return await LoadOrderedAsync(ids.Take(limit).ToList(), ct);
    }

    private async Task<List<Guid>> RankRelatedAsync(Guid videoId, Guid ownerId, string title, CancellationToken ct)
    {
        var tokens = SearchNormalizer.SignificantTokens(title);
        var currentTokens = tokens.ToHashSet();
        var pool = new Dictionary<Guid, Candidate>();

        void Add(IEnumerable<Candidate> items)
        {
            foreach (var c in items)
                if (c.Id != videoId) pool[c.Id] = c;
        }

        // (a) videos sharing the most distinctive title words (longest words first)
        foreach (var t in tokens.OrderByDescending(t => t.Length).Take(4))
        {
            var token = t;
            Add(await PublicReady()
                .Where(v => v.Id != videoId && v.SearchTitle!.Contains(token))
                .OrderByDescending(v => v.ViewCount)
                .Take(50)
                .Select(ToCandidate)
                .ToListAsync(ct));
        }

        // (b) more from the same channel
        Add(await PublicReady()
            .Where(v => v.OwnerId == ownerId && v.Id != videoId)
            .OrderByDescending(v => v.PublishedAt)
            .Take(30)
            .Select(ToCandidate)
            .ToListAsync(ct));

        // (c) co-watching: what else did people who watched this video watch?
        var coWatched = await _db.WatchHistory.AsNoTracking()
            .Where(h => h.VideoId != videoId && _db.WatchHistory.Any(x => x.VideoId == videoId && x.UserId == h.UserId))
            .GroupBy(h => h.VideoId)
            .Select(g => new { VideoId = g.Key, Viewers = g.Count() })
            .OrderByDescending(x => x.Viewers)
            .Take(50)
            .ToListAsync(ct);

        var coMap = coWatched.ToDictionary(x => x.VideoId, x => x.Viewers);
        var missing = coMap.Keys.Where(id => !pool.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            Add(await PublicReady().Where(v => missing.Contains(v.Id)).Select(ToCandidate).ToListAsync(ct));

        var now = DateTime.UtcNow;
        var ranked = pool.Values
            .Select(c =>
            {
                var other = SearchNormalizer.SignificantTokens(c.SearchTitle);
                var shared = other.Count(currentTokens.Contains);
                var union = currentTokens.Count + other.Count - shared;

                var score = (union == 0 ? 0 : 40.0 * shared / union)        // title similarity
                            + (c.OwnerId == ownerId ? 15 : 0)                // same channel
                            + (coMap.TryGetValue(c.Id, out var n) ? Math.Min(n, 10) * 4 : 0) // co-watching
                            + Popularity(c)
                            + Freshness(c, now, tauDays: 120, weight: 8);
                return new { c.Id, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Id)
            .Take(60)
            .ToList();

        // Small libraries: top up with popular videos so the sidebar is never empty
        if (ranked.Count < 20)
        {
            var exclude = ranked.Append(videoId).ToList();
            ranked.AddRange(await PopularIdsAsync(exclude, 20 - ranked.Count, ct));
        }

        return ranked;
    }

    // ======================= trending =======================

    public async Task<PagedResult<VideoListItemDto>> GetTrendingAsync(TrendingWindow window, PagingQuery query, CancellationToken ct = default) =>
        await PageAsync(await GetTrendingIdsAsync(window, ct), query, ct);

    private async Task<List<Guid>> GetTrendingIdsAsync(TrendingWindow window, CancellationToken ct) =>
        await _cache.GetOrCreateAsync($"trending:{window}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = FeedTtl;
            return RankTrendingAsync(window, ct);
        }) ?? [];

    private async Task<List<Guid>> RankTrendingAsync(TrendingWindow window, CancellationToken ct)
    {
        var since = DateTime.UtcNow - window switch
        {
            TrendingWindow.Day => TimeSpan.FromDays(1),
            TrendingWindow.Month => TimeSpan.FromDays(30),
            _ => TimeSpan.FromDays(7)
        };

        // Counted views and likes that happened inside the window
        var views = await _db.VideoViews.AsNoTracking()
            .Where(x => x.CreatedAt >= since)
            .GroupBy(x => x.VideoId)
            .Select(g => new { VideoId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(300)
            .ToListAsync(ct);

        var likes = await _db.VideoReactions.AsNoTracking()
            .Where(r => r.Type == ReactionType.Like && r.CreatedAt >= since)
            .GroupBy(r => r.VideoId)
            .Select(g => new { VideoId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(300)
            .ToListAsync(ct);

        var score = new Dictionary<Guid, double>();
        foreach (var v in views) score[v.VideoId] = v.Count;
        foreach (var l in likes) score[l.VideoId] = score.GetValueOrDefault(l.VideoId) + 5.0 * l.Count;

        var ranked = new List<Guid>();
        if (score.Count > 0)
        {
            var ids = score.Keys.ToList();
            var eligible = await PublicReady()
                .Where(v => ids.Contains(v.Id))
                .Select(v => new { v.Id, v.PublishedAt })
                .ToListAsync(ct);

            ranked = eligible
                .OrderByDescending(v => score[v.Id])
                .ThenByDescending(v => v.PublishedAt)
                .Select(v => v.Id)
                .Take(MaxRankedIds)
                .ToList();
        }

        // Quiet site / short window: continue with the all-time most popular videos
        if (ranked.Count < MaxRankedIds)
            ranked.AddRange(await PopularIdsAsync(ranked, MaxRankedIds - ranked.Count, ct));

        return ranked;
    }

    // ======================= home feed =======================

    public async Task<PagedResult<VideoListItemDto>> GetRecommendedAsync(Guid? userId, PagingQuery query, CancellationToken ct = default)
    {
        if (userId is not Guid uid)
            return await GetTrendingAsync(TrendingWindow.Week, query, ct);

        var ids = await _cache.GetOrCreateAsync($"feed:{uid}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = FeedTtl;
            return RankForUserAsync(uid, ct);
        }) ?? [];

        return await PageAsync(ids, query, ct);
    }

    private async Task<List<Guid>> RankForUserAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var liked = await _db.VideoReactions.AsNoTracking()
            .Where(r => r.UserId == userId && r.Type == ReactionType.Like)
            .OrderByDescending(r => r.CreatedAt).Take(50)
            .Select(r => r.VideoId).ToListAsync(ct);

        var disliked = await _db.VideoReactions.AsNoTracking()
            .Where(r => r.UserId == userId && r.Type == ReactionType.Dislike)
            .OrderByDescending(r => r.CreatedAt).Take(100)
            .Select(r => r.VideoId).ToListAsync(ct);

        var history = await _db.WatchHistory.AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.LastWatchedAt).Take(100)
            .Select(h => new { h.VideoId, h.Completed, h.LastPositionSeconds })
            .ToListAsync(ct);

        // How strongly each video says "I like this kind of thing"
        var weights = new Dictionary<Guid, double>();
        foreach (var id in liked) weights[id] = weights.GetValueOrDefault(id) + 3;
        foreach (var h in history)
        {
            if (h.Completed) weights[h.VideoId] = weights.GetValueOrDefault(h.VideoId) + 2;
            else if (h.LastPositionSeconds >= 30) weights[h.VideoId] = weights.GetValueOrDefault(h.VideoId) + 1;
        }

        // Nothing to learn from yet: behave like the anonymous feed
        if (weights.Count == 0)
            return await GetTrendingIdsAsync(TrendingWindow.Week, ct);

        var seedIds = weights.Keys.ToList();
        var seeds = await _db.Videos.AsNoTracking()
            .Where(v => seedIds.Contains(v.Id))
            .Select(v => new { v.Id, v.OwnerId, v.SearchTitle })
            .ToListAsync(ct);

        var dislikedSeeds = disliked.Count == 0
            ? []
            : await _db.Videos.AsNoTracking()
                .Where(v => disliked.Contains(v.Id))
                .Select(v => new { v.Id, v.OwnerId, v.SearchTitle })
                .ToListAsync(ct);

        // Interest profile: words and channels, positive from likes/watching, negative from dislikes
        var tokenWeight = new Dictionary<string, double>();
        var ownerWeight = new Dictionary<Guid, double>();

        foreach (var s in seeds)
        {
            var w = weights[s.Id];
            foreach (var t in SearchNormalizer.SignificantTokens(s.SearchTitle))
                tokenWeight[t] = tokenWeight.GetValueOrDefault(t) + w;
            ownerWeight[s.OwnerId] = ownerWeight.GetValueOrDefault(s.OwnerId) + w;
        }

        foreach (var s in dislikedSeeds)
        {
            foreach (var t in SearchNormalizer.SignificantTokens(s.SearchTitle))
                tokenWeight[t] = tokenWeight.GetValueOrDefault(t) - 2;
            ownerWeight[s.OwnerId] = ownerWeight.GetValueOrDefault(s.OwnerId) - 3;
        }

        var maxToken = Math.Max(1, tokenWeight.Values.DefaultIfEmpty(0).Max());
        var maxOwner = Math.Max(1, ownerWeight.Values.DefaultIfEmpty(0).Max());

        // Candidate generation
        var pool = new Dictionary<Guid, Candidate>();
        void Add(IEnumerable<Candidate> items)
        {
            foreach (var c in items) pool[c.Id] = c;
        }

        foreach (var t in tokenWeight.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(8).Select(x => x.Key))
        {
            var token = t;
            Add(await PublicReady().Where(v => v.SearchTitle!.Contains(token))
                .OrderByDescending(v => v.ViewCount).Take(40).Select(ToCandidate).ToListAsync(ct));
        }

        foreach (var o in ownerWeight.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(5).Select(x => x.Key))
        {
            var owner = o;
            Add(await PublicReady().Where(v => v.OwnerId == owner)
                .OrderByDescending(v => v.PublishedAt).Take(20).Select(ToCandidate).ToListAsync(ct));
        }

        // Exploration: some fresh and some popular videos outside the profile
        Add(await PublicReady().OrderByDescending(v => v.PublishedAt).Take(40).Select(ToCandidate).ToListAsync(ct));
        Add(await PublicReady().OrderByDescending(v => v.ViewCount).Take(40).Select(ToCandidate).ToListAsync(ct));

        // Do not recommend what the user already finished, liked, disliked, or uploaded
        var skip = new HashSet<Guid>(liked);
        skip.UnionWith(disliked);
        skip.UnionWith(history.Where(h => h.Completed).Select(h => h.VideoId));
        var candidates = pool.Values.Where(c => !skip.Contains(c.Id) && c.OwnerId != userId).ToList();

        if (candidates.Count == 0)
            return await GetTrendingIdsAsync(TrendingWindow.Week, ct);

        var sorted = candidates
            .Select(c =>
            {
                var interest = SearchNormalizer.SignificantTokens(c.SearchTitle)
                    .Sum(t => tokenWeight.GetValueOrDefault(t)) / maxToken;
                var owner = ownerWeight.GetValueOrDefault(c.OwnerId) / maxOwner;

                var score = 12 * Math.Clamp(interest, -2, 4)
                            + 15 * Math.Clamp(owner, -1, 1)
                            + Popularity(c)
                            + Freshness(c, now, tauDays: 60, weight: 10);
                return new { Candidate = c, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Candidate)
            .ToList();

        // Variety: at most N per channel at the top, the rest follows afterwards
        var picked = new List<Guid>();
        var deferred = new List<Guid>();
        var perOwner = new Dictionary<Guid, int>();
        foreach (var c in sorted)
        {
            var n = perOwner.GetValueOrDefault(c.OwnerId);
            if (n < MaxPerOwnerInTopResults)
            {
                picked.Add(c.Id);
                perOwner[c.OwnerId] = n + 1;
            }
            else
            {
                deferred.Add(c.Id);
            }
        }

        picked.AddRange(deferred);
        return picked.Take(MaxRankedIds).ToList();
    }

    // ======================= continue watching =======================

    public async Task<IReadOnlyList<WatchHistoryItemDto>> GetContinueWatchingAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 50);

        var rows = await _db.WatchHistory.AsNoTracking()
            .Where(h => h.UserId == userId
                        && !h.Completed
                        && h.LastPositionSeconds >= 5
                        && h.Video.Status == VideoStatus.Ready
                        && (h.Video.Visibility != VideoVisibility.Private || h.Video.OwnerId == userId))
            .OrderByDescending(h => h.LastWatchedAt)
            .Take(limit)
            .Select(h => new { h.VideoId, h.LastPositionSeconds, h.LastWatchedAt })
            .ToListAsync(ct);

        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.VideoId).ToList();
        var videos = (await _db.Videos.AsNoTracking().Where(v => ids.Contains(v.Id)).Select(VideoProjections.ListItem).ToListAsync(ct))
            .ToDictionary(v => v.Id);

        return rows
            .Where(r => videos.ContainsKey(r.VideoId))
            .Select(r => new WatchHistoryItemDto
            {
                Video = videos[r.VideoId],
                PositionSeconds = Math.Floor(r.LastPositionSeconds),
                Completed = false,
                WatchedAt = r.LastWatchedAt
            })
            .ToList();
    }

    // ======================= helpers =======================

    private IQueryable<Video> PublicReady() =>
        _db.Videos.AsNoTracking().Where(v => v.Status == VideoStatus.Ready && v.Visibility == VideoVisibility.Public);

    private async Task<List<Guid>> PopularIdsAsync(ICollection<Guid> exclude, int take, CancellationToken ct)
    {
        var excluded = exclude.ToList();
        return await PublicReady()
            .Where(v => !excluded.Contains(v.Id))
            .OrderByDescending(v => v.ViewCount).ThenByDescending(v => v.PublishedAt)
            .Take(take)
            .Select(v => v.Id)
            .ToListAsync(ct);
    }

    private static double Popularity(Candidate c) => Math.Log(1 + c.ViewCount) * 1.5 + Math.Log(1 + c.LikeCount) * 2;

    private static double Freshness(Candidate c, DateTime now, double tauDays, double weight) =>
        c.PublishedAt is DateTime p ? weight * Math.Exp(-Math.Max(0, (now - p).TotalDays) / tauDays) : 0;

    /// <summary>Loads cards in the given order. Re-checks visibility, because the id lists are cached for a few minutes.</summary>
    private async Task<List<VideoListItemDto>> LoadOrderedAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        var wanted = ids.ToList();
        var items = await _db.Videos.AsNoTracking()
            .Where(v => wanted.Contains(v.Id) && v.Status == VideoStatus.Ready && v.Visibility == VideoVisibility.Public)
            .Select(VideoProjections.ListItem)
            .ToListAsync(ct);

        var byId = items.ToDictionary(i => i.Id);
        return wanted.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    private async Task<PagedResult<VideoListItemDto>> PageAsync(List<Guid> ids, PagingQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 50);

        var slice = ids.Skip((page - 1) * size).Take(size).ToList();

        return new PagedResult<VideoListItemDto>
        {
            Items = await LoadOrderedAsync(slice, ct),
            Page = page,
            PageSize = size,
            TotalCount = ids.Count
        };
    }
}
