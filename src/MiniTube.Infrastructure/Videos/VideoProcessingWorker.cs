using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniTube.Application.Videos;
using MiniTube.Domain.Enums;
using MiniTube.Infrastructure.Options;
using MiniTube.Infrastructure.Persistence;

namespace MiniTube.Infrastructure.Videos;

public class VideoProcessingWorker : BackgroundService
{
    private readonly VideoProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly FfmpegTools _ffmpeg;
    private readonly VideoOptions _o;
    private readonly ILogger<VideoProcessingWorker> _logger;

    public VideoProcessingWorker(
        VideoProcessingQueue queue,
        IServiceScopeFactory scopes,
        FfmpegTools ffmpeg,
        IOptions<VideoOptions> options,
        ILogger<VideoProcessingWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _ffmpeg = ffmpeg;
        _o = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Don't block host startup
        await Task.Yield();

        if (await _ffmpeg.CheckAvailabilityAsync(ct))
            _logger.LogInformation("FFmpeg found. Video processing workers: {Count}", Math.Max(1, _o.MaxConcurrency));
        else
            _logger.LogError("FFmpeg/ffprobe NOT found: uploaded videos will fail until it is installed.");

        await RecoverAsync(ct);

        var workers = Enumerable.Range(0, Math.Max(1, _o.MaxConcurrency)).Select(_ => RunWorkerAsync(ct));
        await Task.WhenAll(workers);
    }

    private async Task RunWorkerAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var videoId in _queue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IVideoProcessor>();
                    await processor.ProcessAsync(videoId, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker failed on video {VideoId}", videoId);
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    /// <summary>After a crash/restart: reset half-done states and re-queue everything that was waiting.</summary>
    private async Task RecoverAsync(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await db.Videos.Where(v => v.Status == VideoStatus.Processing)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, VideoStatus.Queued), ct);
                await db.Videos.Where(v => v.Status == VideoStatus.Uploading)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, VideoStatus.PendingUpload), ct);

                var ids = await db.Videos.Where(v => v.Status == VideoStatus.Queued)
                    .OrderBy(v => v.CreatedAt).Select(v => v.Id).ToListAsync(ct);

                foreach (var id in ids) await _queue.EnqueueAsync(id, ct);
                if (ids.Count > 0) _logger.LogInformation("Re-queued {Count} video(s) after startup", ids.Count);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Video recovery failed (attempt {Attempt}/3). Have the migrations been applied?", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}
