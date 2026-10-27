namespace MiniTube.Domain.Entities;

public class PlaylistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;

    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;

    /// <summary>Sort key: strictly increasing within a playlist but MAY have gaps (e.g. after a video is deleted).</summary>
    public int Position { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
