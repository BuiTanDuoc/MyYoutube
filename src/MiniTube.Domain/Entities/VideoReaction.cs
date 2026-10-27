using MiniTube.Domain.Enums;

namespace MiniTube.Domain.Entities;

/// <summary>One like/dislike per user per video. CreatedAt is the time of the CURRENT reaction.</summary>
public class VideoReaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;
    public ReactionType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
