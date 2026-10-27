using System.Buffers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniTube.Application.Common;
using MiniTube.Application.Videos;
using MiniTube.Domain.Entities;
using MiniTube.Domain.Enums;
using MiniTube.Infrastructure.Options;
using MiniTube.Infrastructure.Persistence;
using MiniTube.Infrastructure.Storage;

namespace MiniTube.Infrastructure.Videos;

public class VideoService : IVideoService
{
    private readonly AppDbContext _db;
    private readonly IVideoProcessingQueue _queue;
    private readonly StoragePaths _paths;
    private readonly VideoOptions _o;
    private readonly ILogger<VideoService> _logger;

    public VideoService(
        AppDbContext db,
        IVideoProcessingQueue queue,
        StoragePaths paths,
        IOptions<VideoOptions> options,
        ILogger<VideoService> logger)
    {
        _db = db;
        _queue = queue;
        _paths = paths;
        _o = options.Value;
        _logger = logger;
    }

    // ---------------- create / upload ----------------

    public async Task<VideoDto> CreateAsync(Guid ownerId, CreateVideoRequest request, CancellationToken ct = default)
    {
        var video = new Video
        {
            OwnerId = ownerId,
            Title = request.Title.Trim(),
            Description = Clean(request.Description),
            Visibility = request.Visibility
        };
        _db.Videos.Add(video);
        await _db.SaveChangesAsync(ct);

        return await LoadDtoAsync(video.Id, includePrivateFields: true, ct);
    }

    public async Task<VideoDto> UploadSourceAsync(
        Guid ownerId, Guid videoId, string? fileName, long? contentLength, Stream body, CancellationToken ct = default)
    {
        var info = await _db.Videos.AsNoTracking()
            .Where(v => v.Id == videoId)
            .Select(v => new { v.OwnerId, v.Status })
            .SingleOrDefaultAsync(ct);

        if (info is null || info.OwnerId != ownerId)
            throw AppException.NotFound("Video not found");

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!_o.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            throw AppException.BadRequest("Unsupported file type. Allowed: " + string.Join(", ", _o.AllowedExtensions));

        if (contentLength is > 0 && contentLength > _o.MaxUploadBytes)
            throw AppException.PayloadTooLarge($"The file is larger than {FormatBytes(_o.MaxUploadBytes)}.");

        var previousStatus = info.Status == VideoStatus.Failed ? VideoStatus.Failed : VideoStatus.PendingUpload;

        // Atomic claim: blocks a second upload (or an upload while processing) for the same video
        var claimed = await _db.Videos
            .Where(v => v.Id == videoId && (v.Status == VideoStatus.PendingUpload || v.Status == VideoStatus.Failed))
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, VideoStatus.Uploading), ct);

        if (claimed == 0)
            throw AppException.Conflict("This video already has a file that is uploading or being processed.", "upload_not_allowed");

        var dir = _paths.VideoSourceDir(videoId);
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.EnumerateFiles(dir)) File.Delete(old);

        var target = Path.Combine(dir, "original" + ext);
        long total = 0;

        try
        {
            await using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                var buffer = ArrayPool<byte>.Shared.Rent(1 << 20);
                try
                {
                    int read;
                    while ((read = await body.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                    {
                        total += read;
                        if (total > _o.MaxUploadBytes)
                            throw AppException.PayloadTooLarge($"The file is larger than {FormatBytes(_o.MaxUploadBytes)}.");
                        await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            if (total == 0)
                throw AppException.BadRequest("The file is empty.");
        }
        catch
        {
            // Client aborted, too large, disk error...: leave no partial file and release the claim
            TryDeleteFile(target);
            await _db.Videos.Where(v => v.Id == videoId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Status, previousStatus), CancellationToken.None);
            throw;
        }

        var relative = Path.GetRelativePath(_paths.PrivateRoot, target).Replace('\\', '/');
        var originalName = Truncate(Path.GetFileName(fileName ?? string.Empty), 260);
        DateTime? now = DateTime.UtcNow;

        // Finalize even if the client disconnected right after the last byte
        await _db.Videos.Where(v => v.Id == videoId).ExecuteUpdateAsync(s => s
            .SetProperty(v => v.OriginalFileName, originalName)
            .SetProperty(v => v.SourcePath, relative)
            .SetProperty(v => v.SizeBytes, total)
            .SetProperty(v => v.Status, VideoStatus.Queued)
            .SetProperty(v => v.ProcessingProgress, 0)
            .SetProperty(v => v.ProcessingError, (string?)null)
            .SetProperty(v => v.UpdatedAt, now), CancellationToken.None);

        await _queue.EnqueueAsync(videoId, CancellationToken.None);

        return await LoadDtoAsync(videoId, includePrivateFields: true, CancellationToken.None);
    }

    // ---------------- read ----------------

    public async Task<VideoDto> GetAsync(Guid videoId, Guid? requesterId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await _db.Videos.AsNoTracking()
            .Include(v => v.Owner)
            .Include(v => v.Renditions)
            .SingleOrDefaultAsync(v => v.Id == videoId, ct)
            ?? throw AppException.NotFound("Video not found");

        var privileged = isAdmin || (requesterId is not null && requesterId == video.OwnerId);
        if (!privileged && !IsPubliclyViewable(video))
            throw AppException.NotFound("Video not found");

        return video.ToDto(includePrivateFields: privileged);
    }

    public Task<PagedResult<VideoListItemDto>> ListPublicAsync(VideoListQuery query, CancellationToken ct = default)
    {
        var q = _db.Videos.AsNoTracking()
            .Where(v => v.Status == VideoStatus.Ready && v.Visibility == VideoVisibility.Public);

        if (query.OwnerId is { } owner) q = q.Where(v => v.OwnerId == owner);

        return PageAsync(q.OrderByDescending(v => v.PublishedAt), query, ct);
    }

    public Task<PagedResult<VideoListItemDto>> ListMineAsync(Guid ownerId, VideoListQuery query, CancellationToken ct = default)
    {
        var q = _db.Videos.AsNoTracking().Where(v => v.OwnerId == ownerId);
        if (query.Status is { } status) q = q.Where(v => v.Status == status);

        return PageAsync(q.OrderByDescending(v => v.CreatedAt), query, ct);
    }

    // ---------------- update / delete / reprocess ----------------

    public async Task<VideoDto> UpdateAsync(Guid requesterId, bool isAdmin, Guid videoId, UpdateVideoRequest request, CancellationToken ct = default)
    {
        var video = await _db.Videos.SingleOrDefaultAsync(v => v.Id == videoId, ct)
                    ?? throw AppException.NotFound("Video not found");
        EnsureCanManage(video, requesterId, isAdmin);

        video.Title = request.Title.Trim();
        video.Description = Clean(request.Description);
        video.Visibility = request.Visibility;
        video.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await LoadDtoAsync(videoId, includePrivateFields: true, ct);
    }

    public async Task DeleteAsync(Guid requesterId, bool isAdmin, Guid videoId, CancellationToken ct = default)
    {
        var video = await _db.Videos.SingleOrDefaultAsync(v => v.Id == videoId, ct)
                    ?? throw AppException.NotFound("Video not found");
        EnsureCanManage(video, requesterId, isAdmin);

        if (video.Status is VideoStatus.Uploading or VideoStatus.Processing)
            throw AppException.Conflict("The video is being uploaded or processed. Try again when it has finished.", "video_busy");

        _db.Videos.Remove(video); // renditions are removed by the cascade
        await _db.SaveChangesAsync(ct);

        TryDeleteDirectory(_paths.VideoPrivateDir(videoId));
        TryDeleteDirectory(_paths.VideoPublicDir(videoId));
    }

    public async Task<VideoDto> ReprocessAsync(Guid requesterId, bool isAdmin, Guid videoId, CancellationToken ct = default)
    {
        var video = await _db.Videos.AsNoTracking().SingleOrDefaultAsync(v => v.Id == videoId, ct)
                    ?? throw AppException.NotFound("Video not found");
        EnsureCanManage(video, requesterId, isAdmin);

        if (video.Status != VideoStatus.Failed)
            throw AppException.Conflict("Only failed videos can be reprocessed.", "not_failed");

        var hasSource = !string.IsNullOrEmpty(video.SourcePath) &&
                        File.Exists(Path.Combine(_paths.PrivateRoot, video.SourcePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!hasSource)
            throw AppException.Conflict("The original file is no longer available. Upload it again.", "source_missing");

        var claimed = await _db.Videos
            .Where(v => v.Id == videoId && v.Status == VideoStatus.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Status, VideoStatus.Queued)
                .SetProperty(v => v.ProcessingProgress, 0)
                .SetProperty(v => v.ProcessingError, (string?)null), ct);

        if (claimed == 1) await _queue.EnqueueAsync(videoId, ct);

        return await LoadDtoAsync(videoId, includePrivateFields: true, ct);
    }

    // ---------------- helpers ----------------

    private async Task<VideoDto> LoadDtoAsync(Guid id, bool includePrivateFields, CancellationToken ct)
    {
        var video = await _db.Videos.AsNoTracking()
            .Include(v => v.Owner)
            .Include(v => v.Renditions)
            .SingleAsync(v => v.Id == id, ct);
        return video.ToDto(includePrivateFields);
    }

    private static bool IsPubliclyViewable(Video v) =>
        v.Status == VideoStatus.Ready && v.Visibility is VideoVisibility.Public or VideoVisibility.Unlisted;

    private static void EnsureCanManage(Video v, Guid requesterId, bool isAdmin)
    {
        if (isAdmin || v.OwnerId == requesterId) return;
        throw IsPubliclyViewable(v) ? AppException.Forbidden("You can only change your own videos.")
                                    : AppException.NotFound("Video not found");
    }

    private static async Task<PagedResult<VideoListItemDto>> PageAsync(
        IQueryable<Video> ordered, VideoListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip((page - 1) * size)
            .Take(size)
            .Select(v => new VideoListItemDto
            {
                Id = v.Id,
                Title = v.Title,
                ThumbnailUrl = v.ThumbnailUrl,
                DurationSeconds = v.DurationSeconds,
                Visibility = v.Visibility,
                Status = v.Status,
                ProcessingProgress = v.ProcessingProgress,
                CreatedAt = v.CreatedAt,
                PublishedAt = v.PublishedAt,
                Owner = new VideoOwnerDto
                {
                    Id = v.Owner.Id,
                    UserName = v.Owner.UserName ?? string.Empty,
                    DisplayName = v.Owner.DisplayName,
                    AvatarUrl = v.Owner.AvatarUrl
                }
            })
            .ToListAsync(ct);

        return new PagedResult<VideoListItemDto> { Items = items, Page = page, PageSize = size, TotalCount = total };
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static string FormatBytes(long bytes) =>
        bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.#} GB" : $"{bytes / (double)(1L << 20):0.#} MB";

    private void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete {Path}", path); }
    }

    private void TryDeleteDirectory(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete {Dir}", dir); }
    }
}
