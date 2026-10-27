namespace MiniTube.Domain.Entities;

/// <summary>One HLS variant of a video (e.g. 720p).</summary>
public class VideoRendition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;

    public string Name { get; set; } = string.Empty; // "720p"
    public int Width { get; set; }
    public int Height { get; set; }
    public int VideoBitrateKbps { get; set; }
    public int AudioBitrateKbps { get; set; }
    public long SizeBytes { get; set; }
}
