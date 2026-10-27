using MiniTube.Domain.Enums;

namespace MiniTube.Domain.Entities;

public class Video
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VideoVisibility Visibility { get; set; } = VideoVisibility.Private;
    public VideoStatus Status { get; set; } = VideoStatus.PendingUpload;

    // Original upload (kept in the PRIVATE storage root, never served directly)
    public string? OriginalFileName { get; set; }
    public string? SourcePath { get; set; }   // relative to the private root
    public long SizeBytes { get; set; }

    // Filled by the processor
    public double DurationSeconds { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? ThumbnailUrl { get; set; } // relative public URL, e.g. /media/videos/{id}/thumb.jpg

    // Processing bookkeeping
    public int ProcessingProgress { get; set; }  // 0-100
    public string? ProcessingError { get; set; }
    public int ProcessingAttempts { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; } // set when processing completes

    public ICollection<VideoRendition> Renditions { get; set; } = new List<VideoRendition>();
}
