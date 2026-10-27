using System.Threading.Channels;
using MiniTube.Application.Videos;

namespace MiniTube.Infrastructure.Videos;

/// <summary>In-memory wake-up queue. The database (Video.Status) is the source of truth, so nothing is lost on restart.</summary>
public class VideoProcessingQueue : IVideoProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ChannelReader<Guid> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Guid videoId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(videoId, ct);
}
