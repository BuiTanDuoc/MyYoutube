using System.ComponentModel.DataAnnotations;
using MiniTube.Domain.Enums;

namespace MiniTube.Application.Videos;

public class CreateVideoRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; set; }

    public VideoVisibility Visibility { get; set; } = VideoVisibility.Private;
}

public class UpdateVideoRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; set; }

    public VideoVisibility Visibility { get; set; }
}

public class VideoOwnerDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class RenditionDto
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int BitrateKbps { get; set; }
}

public class VideoDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VideoVisibility Visibility { get; set; }
    public VideoStatus Status { get; set; }
    public VideoOwnerDto Owner { get; set; } = null!;

    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Relative URL (prefix with the API base URL).</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>HLS master playlist, relative URL. Only set when Status = Ready.</summary>
    public string? StreamUrl { get; set; }

    public List<RenditionDto> Renditions { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public long ViewCount { get; set; }
    public int LikeCount { get; set; }

    // Only filled for a signed-in viewer
    public ReactionType? MyReaction { get; set; }

    /// <summary>Where to resume playback (null = start from the beginning).</summary>
    public double? ResumePositionSeconds { get; set; }

    // Owner / admin only (null for everyone else)
    public int? DislikeCount { get; set; }
    public long? SizeBytes { get; set; }
    public int? ProcessingProgress { get; set; }
    public string? ProcessingError { get; set; }
}

public class VideoListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public double DurationSeconds { get; set; }
    public VideoVisibility Visibility { get; set; }
    public VideoStatus Status { get; set; }
    public int ProcessingProgress { get; set; }
    public long ViewCount { get; set; }
    public VideoOwnerDto Owner { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class VideoListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Public list: only videos of this user (channel page).</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>"Mine" list: filter by status.</summary>
    public VideoStatus? Status { get; set; }
}

public record StreamFileResult(string PhysicalPath, string ContentType, bool IsPrivate);

public static class VideoRoutes
{
    public static string Master(Guid videoId) => $"/api/stream/{videoId}/master.m3u8";
}
