namespace MiniTube.Infrastructure.Videos;

/// <summary>An expected failure whose message is safe to show to the video owner.</summary>
public class VideoProcessingException : Exception
{
    public VideoProcessingException(string message) : base(message) { }
}
