namespace MiniTube.Domain.Entities;

/// <summary>Per-user watch progress: powers "resume where you left off" and the history page.</summary>
public class WatchHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;

    public double LastPositionSeconds { get; set; }
    public bool Completed { get; set; }
    public DateTime LastWatchedAt { get; set; } = DateTime.UtcNow;
}
