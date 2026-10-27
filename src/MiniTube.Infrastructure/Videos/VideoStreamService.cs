using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MiniTube.Application.Common;
using MiniTube.Application.Videos;
using MiniTube.Domain.Enums;
using MiniTube.Infrastructure.Persistence;
using MiniTube.Infrastructure.Storage;

namespace MiniTube.Infrastructure.Videos;

public class VideoStreamService : IVideoStreamService
{
    // Whitelist: nothing but the files the processor creates. This is also the path-traversal guard.
    private static readonly Regex AllowedPath = new(
        @"^(master\.m3u8|[0-9]{2,4}p/index\.m3u8|[0-9]{2,4}p/seg_[0-9]{5,}\.ts)\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AppDbContext _db;
    private readonly StoragePaths _paths;

    public VideoStreamService(AppDbContext db, StoragePaths paths)
    {
        _db = db;
        _paths = paths;
    }

    public async Task<StreamFileResult> GetFileAsync(
        Guid videoId, string relativePath, Guid? requesterId, bool isAdmin, CancellationToken ct = default)
    {
        if (!AllowedPath.IsMatch(relativePath))
            throw AppException.NotFound();

        var video = await _db.Videos.AsNoTracking()
            .Where(v => v.Id == videoId)
            .Select(v => new { v.OwnerId, v.Visibility, v.Status })
            .SingleOrDefaultAsync(ct);

        if (video is null || video.Status != VideoStatus.Ready)
            throw AppException.NotFound();

        var isPrivate = video.Visibility == VideoVisibility.Private;
        var privileged = isAdmin || (requesterId is not null && requesterId == video.OwnerId);

        // 404 (not 403) so the existence of private videos is not revealed
        if (isPrivate && !privileged)
            throw AppException.NotFound();

        var fullPath = Path.Combine(_paths.VideoHlsDir(videoId), relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
            throw AppException.NotFound();

        var contentType = relativePath.EndsWith(".m3u8", StringComparison.Ordinal)
            ? "application/vnd.apple.mpegurl"
            : "video/mp2t";

        return new StreamFileResult(fullPath, contentType, isPrivate);
    }
}
