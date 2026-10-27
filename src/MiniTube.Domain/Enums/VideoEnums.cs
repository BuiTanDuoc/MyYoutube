namespace MiniTube.Domain.Enums;

public enum VideoVisibility
{
    Private = 0,   // owner (and admins) only
    Unlisted = 1,  // anyone with the link, not listed
    Public = 2     // listed and playable by everyone
}

public enum VideoStatus
{
    PendingUpload = 0, // metadata created, waiting for the file
    Uploading = 1,
    Queued = 2,        // file received, waiting for the background worker
    Processing = 3,    // FFmpeg is working
    Ready = 4,
    Failed = 5
}

public enum ReactionType
{
    Like = 1,
    Dislike = 2
}

public enum PlaylistKind
{
    Custom = 0,
    WatchLater = 1 // built-in, one per user, always private, cannot be renamed or deleted
}
