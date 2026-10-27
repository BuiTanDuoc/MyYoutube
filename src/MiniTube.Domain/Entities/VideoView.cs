namespace MiniTube.Domain.Entities;

/// <summary>
/// One COUNTED view. Used to avoid counting the same viewer twice within 24 hours.
/// Signed-in viewers are identified by UserId; anonymous viewers (and users who paused their
/// watch history) only by a one-way hash of IP + user agent, with UserId left null.
/// </summary>
public class VideoView
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string? ViewerKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
