using MiniTube.Domain.Enums;

namespace MiniTube.Domain.Entities;

public class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Same three levels as videos: Private (owner), Unlisted (link), Public (listed).</summary>
    public VideoVisibility Visibility { get; set; } = VideoVisibility.Private;
    public PlaylistKind Kind { get; set; } = PlaylistKind.Custom;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<PlaylistItem> Items { get; set; } = new List<PlaylistItem>();
}
