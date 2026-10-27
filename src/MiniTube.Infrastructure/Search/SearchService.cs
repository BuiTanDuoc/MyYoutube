using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniTube.Application.Common;
using MiniTube.Application.Search;
using MiniTube.Application.Videos;
using MiniTube.Domain.Entities;
using MiniTube.Domain.Enums;
using MiniTube.Infrastructure.Options;
using MiniTube.Infrastructure.Persistence;
using MiniTube.Infrastructure.Videos;

namespace MiniTube.Infrastructure.Search;

public class SearchService : ISearchService
{
    /// <summary>Relevance ranking looks at the best N matches only (like YouTube, results are capped).</summary>
    private const int RelevanceCandidates = 300;

    private const int ShortMaxSeconds = 4 * 60;
    private const int LongMinSeconds = 20 * 60;

    private readonly AppDbContext _db;
    private readonly SearchOptions _o;

    public SearchService(AppDbContext db, IOptions<SearchOptions> options)
    {
        _db = db;
        _o = options.Value;
    }

    // ---------------- videos ----------------

    public async Task<PagedResult<VideoListItemDto>> SearchVideosAsync(VideoSearchQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 50);
        var now = DateTime.UtcNow;

        // Only public, finished videos are searchable (unlisted ones are link-only)
        var q = _db.Videos.AsNoTracking()
            .Where(v => v.Status == VideoStatus.Ready && v.Visibility == VideoVisibility.Public);

        if (query.OwnerId is Guid owner) q = q.Where(v => v.OwnerId == owner);

        if (query.UploadDate != UploadDateFilter.Any)
        {
            var since = now - query.UploadDate switch
            {
                UploadDateFilter.Today => TimeSpan.FromDays(1),
                UploadDateFilter.Week => TimeSpan.FromDays(7),
                UploadDateFilter.Month => TimeSpan.FromDays(30),
                _ => TimeSpan.FromDays(365)
            };
            q = q.Where(v => v.PublishedAt >= since);
        }

        q = query.Duration switch
        {
            DurationFilter.Short => q.Where(v => v.DurationSeconds < ShortMaxSeconds),
            DurationFilter.Medium => q.Where(v => v.DurationSeconds >= ShortMaxSeconds && v.DurationSeconds <= LongMinSeconds),
            DurationFilter.Long => q.Where(v => v.DurationSeconds > LongMinSeconds),
            _ => q
        };

        var tokens = SearchNormalizer.Tokenize(query.Q);
        q = ApplyTextFilter(q, tokens);

        if (query.Sort == SearchSort.Relevance && tokens.Count > 0)
            return await RankedAsync(q, tokens, SearchNormalizer.Normalize(query.Q), page, size, now, ct);

        // No text (or an explicit sort): let the database order and page
        IOrderedQueryable<Video> ordered = query.Sort switch
        {
            SearchSort.MostViewed => q.OrderByDescending(v => v.ViewCount).ThenByDescending(v => v.PublishedAt),
            SearchSort.MostLiked => q.OrderByDescending(v => v.LikeCount).ThenByDescending(v => v.ViewCount),
            _ => q.OrderByDescending(v => v.PublishedAt)
        };

        var total = await ordered.CountAsync(ct);
        var items = await ordered.Skip((page - 1) * size).Take(size).Select(VideoProjections.ListItem).ToListAsync(ct);

        return new PagedResult<VideoListItemDto> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    private async Task<PagedResult<VideoListItemDto>> RankedAsync(
        IQueryable<Video> matches, List<string> tokens, string phrase, int page, int size, DateTime now, CancellationToken ct)
    {
        var total = Math.Min(await matches.CountAsync(ct), RelevanceCandidates);

        // Take the strongest candidates by popularity, then score them in memory
        var candidates = await matches
            .OrderByDescending(v => v.ViewCount).ThenByDescending(v => v.PublishedAt)
            .Take(RelevanceCandidates)
            .Select(v => new { v.Id, v.SearchTitle, v.ViewCount, v.LikeCount, v.PublishedAt })
            .ToListAsync(ct);

        var pageIds = candidates
            .Select(c => new { c.Id, Score = Score(c.SearchTitle ?? string.Empty, tokens, phrase, c.ViewCount, c.LikeCount, c.PublishedAt, now) })
            .OrderByDescending(x => x.Score)
            .Skip((page - 1) * size).Take(size)
            .Select(x => x.Id)
            .ToList();

        var byId = pageIds.Count == 0
            ? new Dictionary<Guid, VideoListItemDto>()
            : (await _db.Videos.AsNoTracking().Where(v => pageIds.Contains(v.Id)).Select(VideoProjections.ListItem).ToListAsync(ct))
                .ToDictionary(v => v.Id);

        return new PagedResult<VideoListItemDto>
        {
            Items = pageIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    /// <summary>
    /// Every candidate already contains ALL tokens somewhere (title or description), so the score
    /// rewards title matches, then adds popularity and a mild freshness bonus.
    /// </summary>
    private static double Score(
        string title, IReadOnlyList<string> tokens, string phrase, long views, int likes, DateTime? published, DateTime now)
    {
        double score = 0;

        if (phrase.Length > 0 && title.Contains(phrase, StringComparison.Ordinal))
        {
            score += 50;
            if (title.StartsWith(phrase, StringComparison.Ordinal)) score += 15;
        }

        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (!title.Contains(token, StringComparison.Ordinal))
            {
                score += 2; // only in the description
                continue;
            }

            score += 10;
            if (words.Any(w => w.StartsWith(token, StringComparison.Ordinal))) score += 5;
            if (words.Contains(token)) score += 3;
        }

        score += Math.Log(1 + views) * 2 + Math.Log(1 + likes) * 3;

        if (published is DateTime p)
            score += 10 * Math.Exp(-Math.Max(0, (now - p).TotalDays) / 90.0);

        return score;
    }

    private IQueryable<Video> ApplyTextFilter(IQueryable<Video> q, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0) return q;

        if (_o.UseFullText)
        {
            // Tokens are letters/digits only (normalised), so they cannot inject CONTAINS syntax
            var expression = string.Join(" AND ", tokens.Select(t => $"\"{t}*\""));
            return q.Where(v => EF.Functions.Contains(v.SearchText!, expression));
        }

        foreach (var t in tokens)
        {
            var token = t;
            q = q.Where(v => v.SearchText!.Contains(token)); // every word must appear (AND)
        }
        return q;
    }

    // ---------------- channels ----------------

    public async Task<PagedResult<ChannelSearchResultDto>> SearchChannelsAsync(ChannelSearchQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 50);

        var tokens = SearchNormalizer.Tokenize(query.Q);
        if (tokens.Count == 0)
            return new PagedResult<ChannelSearchResultDto> { Items = [], Page = page, PageSize = size, TotalCount = 0 };

        // Only users that actually have a public video count as channels
        var users = _db.Users.AsNoTracking()
            .Where(u => _db.Videos.Any(v => v.OwnerId == u.Id && v.Status == VideoStatus.Ready && v.Visibility == VideoVisibility.Public));

        foreach (var t in tokens)
        {
            var token = t;
            users = users.Where(u => u.SearchName!.Contains(token));
        }

        var total = await users.CountAsync(ct);
        var items = await users
            .Select(u => new ChannelSearchResultDto
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                VideoCount = _db.Videos.Count(v => v.OwnerId == u.Id && v.Status == VideoStatus.Ready && v.Visibility == VideoVisibility.Public)
            })
            .OrderByDescending(c => c.VideoCount)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

        return new PagedResult<ChannelSearchResultDto> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    // ---------------- autocomplete ----------------

    public async Task<IReadOnlyList<SearchSuggestionDto>> SuggestAsync(string? q, CancellationToken ct = default)
    {
        var text = SearchNormalizer.Normalize(q);
        if (text.Length == 0 || text.Length > 60) return [];

        var spaced = " " + text;

        // Titles that start with, or contain a word starting with, what was typed (most viewed first)
        var titles = await _db.Videos.AsNoTracking()
            .Where(v => v.Status == VideoStatus.Ready && v.Visibility == VideoVisibility.Public
                        && (v.SearchTitle!.StartsWith(text) || v.SearchTitle.Contains(spaced)))
            .OrderByDescending(v => v.ViewCount)
            .Select(v => v.Title)
            .Take(30)
            .ToListAsync(ct);

        var result = titles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(t => new SearchSuggestionDto { Type = "video", Text = t })
            .ToList();

        var channels = await _db.Users.AsNoTracking()
            .Where(u => (u.SearchName!.StartsWith(text) || u.SearchName.Contains(spaced))
                        && _db.Videos.Any(v => v.OwnerId == u.Id && v.Status == VideoStatus.Ready && v.Visibility == VideoVisibility.Public))
            .Select(u => new { u.Id, u.DisplayName })
            .Take(2)
            .ToListAsync(ct);

        result.AddRange(channels.Select(c => new SearchSuggestionDto { Type = "channel", Text = c.DisplayName, Id = c.Id }));
        return result;
    }
}
