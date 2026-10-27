namespace MiniTube.Application.Videos;

public interface IVideoProcessingQueue
{
    ValueTask EnqueueAsync(Guid videoId, CancellationToken ct = default);
}

public interface IVideoProcessor
{
    /// <summary>Claims a Queued video and runs thumbnail + HLS generation. No-op if it is no longer Queued.</summary>
    Task ProcessAsync(Guid videoId, CancellationToken ct);
}

public interface IVideoStreamService
{
    /// <summary>Authorizes and resolves one HLS file (master playlist, rendition playlist or segment).</summary>
    Task<StreamFileResult> GetFileAsync(Guid videoId, string relativePath, Guid? requesterId, bool isAdmin, CancellationToken ct = default);
}
