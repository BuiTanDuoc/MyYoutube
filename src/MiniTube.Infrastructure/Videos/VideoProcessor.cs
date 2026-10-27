using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniTube.Application.Videos;
using MiniTube.Domain.Entities;
using MiniTube.Domain.Enums;
using MiniTube.Infrastructure.Options;
using MiniTube.Infrastructure.Persistence;
using MiniTube.Infrastructure.Storage;

namespace MiniTube.Infrastructure.Videos;

public class VideoProcessor : IVideoProcessor
{
    // Height, video kbps, audio kbps. Only rungs <= the source height are produced (no upscaling).
    private static readonly (int Height, int VideoKbps, int AudioKbps)[] Ladder =
    [
        (1080, 5000, 192),
        (720, 2800, 128),
        (480, 1400, 128),
        (360, 800, 96)
    ];

    private const int SegmentSeconds = 4;

    private sealed record RenditionPlan(string Name, int Width, int Height, int VideoKbps, int AudioKbps);

    private readonly AppDbContext _db;
    private readonly FfmpegTools _ffmpeg;
    private readonly StoragePaths _paths;
    private readonly VideoOptions _o;
    private readonly ILogger<VideoProcessor> _logger;

    public VideoProcessor(
        AppDbContext db,
        FfmpegTools ffmpeg,
        StoragePaths paths,
        IOptions<VideoOptions> options,
        ILogger<VideoProcessor> logger)
    {
        _db = db;
        _ffmpeg = ffmpeg;
        _paths = paths;
        _o = options.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid videoId, CancellationToken ct)
    {
        // Atomic claim: only one worker can move Queued -> Processing
        DateTime? now = DateTime.UtcNow;
        var claimed = await _db.Videos
            .Where(v => v.Id == videoId && v.Status == VideoStatus.Queued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Status, VideoStatus.Processing)
                .SetProperty(v => v.ProcessingProgress, 0)
                .SetProperty(v => v.ProcessingError, (string?)null)
                .SetProperty(v => v.ProcessingStartedAt, now)
                .SetProperty(v => v.ProcessingAttempts, v => v.ProcessingAttempts + 1), ct);

        if (claimed == 0) return;

        var video = await _db.Videos.AsNoTracking().SingleAsync(v => v.Id == videoId, ct);
        _logger.LogInformation("Processing video {VideoId} ({Title})", video.Id, video.Title);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromMinutes(_o.TimeoutMinutes));

        try
        {
            await RunPipelineAsync(video, limit.Token);
            _logger.LogInformation("Video {VideoId} is ready", video.Id);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Application is shutting down: put it back in the queue, it resumes on next start
            await _db.Videos.Where(v => v.Id == videoId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, VideoStatus.Queued), CancellationToken.None);
            DeleteOutputs(videoId);
            throw;
        }
        catch (OperationCanceledException)
        {
            await FailAsync(videoId, "Processing timed out.");
        }
        catch (VideoProcessingException ex)
        {
            await FailAsync(videoId, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing video {VideoId}", videoId);
            await FailAsync(videoId, "Processing failed unexpectedly. Check the server logs.");
        }
    }

    private async Task RunPipelineAsync(Video video, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(video.SourcePath))
            throw new VideoProcessingException("The uploaded file is missing.");

        var sourceFile = Path.Combine(_paths.PrivateRoot, video.SourcePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(sourceFile))
            throw new VideoProcessingException("The uploaded file is missing.");

        // 1. Inspect
        var probe = await _ffmpeg.ProbeAsync(sourceFile, ct);
        if (probe.DurationSeconds > _o.MaxDurationMinutes * 60.0)
            throw new VideoProcessingException($"Videos longer than {_o.MaxDurationMinutes} minutes are not supported.");
        await SetProgressAsync(video.Id, 5, ct);

        // Fresh output folders (a retry must not mix with leftovers)
        DeleteOutputs(video.Id);
        var hlsDir = _paths.VideoHlsDir(video.Id);
        var publicDir = _paths.VideoPublicDir(video.Id);
        Directory.CreateDirectory(hlsDir);
        Directory.CreateDirectory(publicDir);

        // 2. Thumbnail (16:9, letterboxed, ~25% into the video)
        var thumbFile = Path.Combine(publicDir, "thumb.jpg");
        var seek = probe.DurationSeconds > 2 ? probe.DurationSeconds * 0.25 : 0;
        await _ffmpeg.RunFfmpegAsync(ThumbnailArgs(sourceFile, thumbFile, seek), ct);
        if (!File.Exists(thumbFile)) // seek landed past the last frame
            await _ffmpeg.RunFfmpegAsync(ThumbnailArgs(sourceFile, thumbFile, 0), ct);
        if (!File.Exists(thumbFile))
            throw new VideoProcessingException("Could not create a thumbnail.");
        await SetProgressAsync(video.Id, 10, ct);

        // 3. One HLS variant per rung of the ladder
        var plans = Plan(probe.Width, probe.Height);
        var renditions = new List<VideoRendition>();
        for (var i = 0; i < plans.Count; i++)
        {
            var plan = plans[i];
            var dir = Path.Combine(hlsDir, plan.Name);
            Directory.CreateDirectory(dir);

            await _ffmpeg.RunFfmpegAsync(HlsArgs(sourceFile, dir, plan), ct);

            renditions.Add(new VideoRendition
            {
                VideoId = video.Id,
                Name = plan.Name,
                Width = plan.Width,
                Height = plan.Height,
                VideoBitrateKbps = plan.VideoKbps,
                AudioBitrateKbps = plan.AudioKbps,
                SizeBytes = new DirectoryInfo(dir).EnumerateFiles().Sum(f => f.Length)
            });

            await SetProgressAsync(video.Id, 10 + (int)(85.0 * (i + 1) / plans.Count), ct);
        }

        // 4. Master playlist (lowest first)
        await File.WriteAllTextAsync(Path.Combine(hlsDir, "master.m3u8"), BuildMasterPlaylist(plans), new UTF8Encoding(false), ct);

        var duration = probe.DurationSeconds > 0
            ? probe.DurationSeconds
            : SumPlaylistDuration(Path.Combine(hlsDir, plans[0].Name, "index.m3u8"));

        // 5. Persist
        await _db.VideoRenditions.Where(r => r.VideoId == video.Id).ExecuteDeleteAsync(ct);
        _db.VideoRenditions.AddRange(renditions);
        await _db.SaveChangesAsync(ct);

        DateTime? finished = DateTime.UtcNow;
        var thumbUrl = _paths.VideoThumbUrl(video.Id);
        await _db.Videos.Where(v => v.Id == video.Id).ExecuteUpdateAsync(s => s
            .SetProperty(v => v.Status, VideoStatus.Ready)
            .SetProperty(v => v.ProcessingProgress, 100)
            .SetProperty(v => v.ProcessingError, (string?)null)
            .SetProperty(v => v.DurationSeconds, duration)
            .SetProperty(v => v.Width, probe.Width)
            .SetProperty(v => v.Height, probe.Height)
            .SetProperty(v => v.ThumbnailUrl, thumbUrl)
            .SetProperty(v => v.PublishedAt, video.PublishedAt ?? finished)
            .SetProperty(v => v.UpdatedAt, finished), ct);

        if (!_o.KeepOriginal)
        {
            TryDeleteDirectory(_paths.VideoSourceDir(video.Id));
            await _db.Videos.Where(v => v.Id == video.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.SourcePath, (string?)null), ct);
        }
    }

    // ---------------- FFmpeg argument builders ----------------

    private static List<string> ThumbnailArgs(string source, string output, double seekSeconds) =>
    [
        "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
        "-protocol_whitelist", "file",
        "-ss", seekSeconds.ToString("F3", CultureInfo.InvariantCulture),
        "-i", source,
        "-frames:v", "1",
        "-vf", "scale=640:360:force_original_aspect_ratio=decrease,pad=640:360:(ow-iw)/2:(oh-ih)/2:black",
        "-q:v", "3",
        output
    ];

    private List<string> HlsArgs(string source, string outDir, RenditionPlan p)
    {
        var maxRate = (int)(p.VideoKbps * 1.07);
        var bufSize = (int)(p.VideoKbps * 1.5);

        return
        [
            "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
            "-protocol_whitelist", "file",
            "-i", source,
            "-map", "0:v:0", "-map", "0:a:0?",
            "-vf", $"scale=-2:{p.Height}",
            "-c:v", "libx264", "-preset", _o.X264Preset, "-profile:v", "main", "-pix_fmt", "yuv420p",
            "-b:v", $"{p.VideoKbps}k", "-maxrate", $"{maxRate}k", "-bufsize", $"{bufSize}k",
            // keyframe every segment boundary so all renditions switch cleanly
            "-force_key_frames", $"expr:gte(t,n_forced*{SegmentSeconds})",
            "-sc_threshold", "0",
            "-c:a", "aac", "-b:a", $"{p.AudioKbps}k", "-ac", "2",
            "-f", "hls",
            "-hls_time", SegmentSeconds.ToString(CultureInfo.InvariantCulture),
            "-hls_playlist_type", "vod",
            "-hls_flags", "independent_segments",
            "-hls_segment_filename", Path.Combine(outDir, "seg_%05d.ts"),
            Path.Combine(outDir, "index.m3u8")
        ];
    }

    // ---------------- planning & playlists ----------------

    private static List<RenditionPlan> Plan(int srcWidth, int srcHeight)
    {
        var plans = Ladder
            .Where(l => l.Height <= srcHeight)
            .Select(l => new RenditionPlan($"{l.Height}p", EvenWidth(srcWidth, srcHeight, l.Height), l.Height, l.VideoKbps, l.AudioKbps))
            .OrderBy(p => p.Height)
            .ToList();

        if (plans.Count == 0) // smaller than 360p: a single rendition at native size
        {
            var h = Math.Max(2, srcHeight - srcHeight % 2);
            plans.Add(new RenditionPlan($"{h}p", EvenWidth(srcWidth, srcHeight, h), h, 500, 96));
        }

        return plans;
    }

    private static int EvenWidth(int srcWidth, int srcHeight, int targetHeight) =>
        Math.Max(2, (int)Math.Round(srcWidth * (double)targetHeight / srcHeight / 2.0) * 2);

    private static string BuildMasterPlaylist(IEnumerable<RenditionPlan> plans)
    {
        var sb = new StringBuilder();
        sb.Append("#EXTM3U\n#EXT-X-VERSION:3\n");
        foreach (var p in plans.OrderBy(p => p.Height))
        {
            var bandwidth = (p.VideoKbps + p.AudioKbps) * 1000;
            sb.Append($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={p.Width}x{p.Height},NAME=\"{p.Name}\"\n");
            sb.Append($"{p.Name}/index.m3u8\n");
        }
        return sb.ToString();
    }

    private static double SumPlaylistDuration(string playlistPath)
    {
        double total = 0;
        foreach (var line in File.ReadLines(playlistPath))
        {
            if (!line.StartsWith("#EXTINF:", StringComparison.Ordinal)) continue;
            var value = line["#EXTINF:".Length..].Split(',')[0];
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) total += d;
        }
        return total;
    }

    // ---------------- DB / disk helpers ----------------

    private Task<int> SetProgressAsync(Guid id, int percent, CancellationToken ct) =>
        _db.Videos.Where(v => v.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.ProcessingProgress, percent), ct);

    private async Task FailAsync(Guid id, string message)
    {
        _logger.LogWarning("Video {VideoId} failed: {Message}", id, message);
        DeleteOutputs(id);

        var text = message.Length > 1000 ? message[..1000] : message;
        DateTime? now = DateTime.UtcNow;
        await _db.Videos.Where(v => v.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(v => v.Status, VideoStatus.Failed)
            .SetProperty(v => v.ProcessingError, text)
            .SetProperty(v => v.UpdatedAt, now), CancellationToken.None);
    }

    private void DeleteOutputs(Guid id)
    {
        TryDeleteDirectory(_paths.VideoHlsDir(id));
        TryDeleteDirectory(_paths.VideoPublicDir(id));
    }

    private void TryDeleteDirectory(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete {Dir}", dir); }
    }
}
