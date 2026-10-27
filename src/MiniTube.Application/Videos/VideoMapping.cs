using MiniTube.Domain.Entities;
using MiniTube.Domain.Enums;

namespace MiniTube.Application.Videos;

public static class VideoMapping
{
    /// <summary>Requires Owner and Renditions to be loaded.</summary>
    public static VideoDto ToDto(this Video v, bool includePrivateFields) => new()
    {
        Id = v.Id,
        Title = v.Title,
        Description = v.Description,
        Visibility = v.Visibility,
        Status = v.Status,
        Owner = new VideoOwnerDto
        {
            Id = v.Owner.Id,
            UserName = v.Owner.UserName ?? string.Empty,
            DisplayName = v.Owner.DisplayName,
            AvatarUrl = v.Owner.AvatarUrl
        },
        DurationSeconds = v.DurationSeconds,
        Width = v.Width,
        Height = v.Height,
        ThumbnailUrl = v.ThumbnailUrl,
        StreamUrl = v.Status == VideoStatus.Ready ? VideoRoutes.Master(v.Id) : null,
        Renditions = v.Renditions.OrderBy(r => r.Height).Select(r => new RenditionDto
        {
            Name = r.Name,
            Width = r.Width,
            Height = r.Height,
            BitrateKbps = r.VideoBitrateKbps + r.AudioBitrateKbps
        }).ToList(),
        CreatedAt = v.CreatedAt,
        PublishedAt = v.PublishedAt,
        ViewCount = v.ViewCount,
        LikeCount = v.LikeCount,
        DislikeCount = includePrivateFields ? v.DislikeCount : null,
        SizeBytes = includePrivateFields ? v.SizeBytes : null,
        ProcessingProgress = includePrivateFields ? v.ProcessingProgress : null,
        ProcessingError = includePrivateFields ? v.ProcessingError : null
    };
}
