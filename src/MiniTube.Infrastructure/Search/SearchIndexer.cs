using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniTube.Application.Search;
using MiniTube.Infrastructure.Persistence;

namespace MiniTube.Infrastructure.Search;

public class SearchIndexer : ISearchIndexer
{
    private const int BatchSize = 200;

    private readonly AppDbContext _db;
    private readonly ILogger<SearchIndexer> _logger;

    public SearchIndexer(AppDbContext db, ILogger<SearchIndexer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ReindexResultDto> BackfillMissingAsync(CancellationToken ct = default)
    {
        var result = new ReindexResultDto();

        // Rows leave the "missing" set as soon as they are saved, so no paging cursor is needed
        while (true)
        {
            var batch = await _db.Videos.Where(v => v.SearchTitle == null).OrderBy(v => v.CreatedAt).Take(BatchSize).ToListAsync(ct);
            if (batch.Count == 0) break;

            foreach (var video in batch) SearchNormalizer.ApplyTo(video);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            result.Videos += batch.Count;
        }

        while (true)
        {
            var batch = await _db.Users.Where(u => u.SearchName == null).OrderBy(u => u.CreatedAt).Take(BatchSize).ToListAsync(ct);
            if (batch.Count == 0) break;

            foreach (var user in batch) SearchNormalizer.ApplyTo(user);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            result.Users += batch.Count;
        }

        if (result.Videos + result.Users > 0)
            _logger.LogInformation("Search index backfill: {Videos} videos, {Users} users", result.Videos, result.Users);

        return result;
    }

    public async Task<ReindexResultDto> ReindexAllAsync(CancellationToken ct = default)
    {
        await _db.Videos.ExecuteUpdateAsync(s => s
            .SetProperty(v => v.SearchTitle, (string?)null)
            .SetProperty(v => v.SearchText, (string?)null), ct);
        await _db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.SearchName, (string?)null), ct);

        return await BackfillMissingAsync(ct);
    }
}

/// <summary>Runs once after startup so videos/users that existed before search was added become searchable.</summary>
public class SearchBackfillService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SearchBackfillService> _logger;

    public SearchBackfillService(IServiceScopeFactory scopes, ILogger<SearchBackfillService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct); // let the dev-time migration finish first
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISearchIndexer>().BackfillMissingAsync(ct);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search index backfill failed. Have the migrations been applied? (POST /api/admin/search/reindex retries it)");
        }
    }
}
