using System.Linq.Expressions;
using MiniTube.Application.Videos;
using MiniTube.Domain.Entities;

namespace MiniTube.Infrastructure.Videos;

/// <summary>Reusable EF projection so every list (public, mine, history, liked) returns the same shape.</summary>
public static class VideoProjections
{
    public static readonly Expression<Func<Video, VideoListItemDto>> ListItem = v => new VideoListItemDto
    {
        Id = v.Id,
        Title = v.Title,
        ThumbnailUrl = v.ThumbnailUrl,
        DurationSeconds = v.DurationSeconds,
        Visibility = v.Visibility,
        Status = v.Status,
        ProcessingProgress = v.ProcessingProgress,
        ViewCount = v.ViewCount,
        CreatedAt = v.CreatedAt,
        PublishedAt = v.PublishedAt,
        Owner = new VideoOwnerDto
        {
            Id = v.Owner.Id,
            UserName = v.Owner.UserName ?? string.Empty,
            DisplayName = v.Owner.DisplayName,
            AvatarUrl = v.Owner.AvatarUrl
        }
    };
}
