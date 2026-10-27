using System.ComponentModel.DataAnnotations;
using MiniTube.Application.Videos;
using MiniTube.Domain.Enums;

namespace MiniTube.Application.Playback;

public class ViewResultDto
{
    /// <summary>False when this viewer already counted within the last 24 hours.</summary>
    public bool Counted { get; set; }
    public long ViewCount { get; set; }
}

public class UpdateProgressRequest
{
    [Range(0.0, 604800.0)]
    public double PositionSeconds { get; set; }
}

public class SetReactionRequest
{
    /// <summary>"Like", "Dislike", or null to remove the reaction.</summary>
    public ReactionType? Type { get; set; }
}

public class ReactionStateDto
{
    public ReactionType? MyReaction { get; set; }
    public int LikeCount { get; set; }
}

public class WatchHistoryItemDto
{
    public VideoListItemDto Video { get; set; } = null!;

    /// <summary>Where to resume (0 when the video was finished).</summary>
    public double PositionSeconds { get; set; }
    public bool Completed { get; set; }
    public DateTime WatchedAt { get; set; }
}

public class PagingQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
