using System.ComponentModel.DataAnnotations;
using MiniTube.Application.Videos;
using MiniTube.Domain.Enums;

namespace MiniTube.Application.Playlists;

public class CreatePlaylistRequest
{
    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; set; }

    public VideoVisibility Visibility { get; set; } = VideoVisibility.Private;
}

public class UpdatePlaylistRequest
{
    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; set; }

    public VideoVisibility Visibility { get; set; }
}

public class AddPlaylistItemRequest
{
    [Required]
    public Guid VideoId { get; set; }
}

public class MovePlaylistItemRequest
{
    /// <summary>Put the item right after this item. Null = move to the top.</summary>
    public Guid? AfterItemId { get; set; }
}

public class PlaylistDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VideoVisibility Visibility { get; set; }

    /// <summary>"WatchLater" playlists have a fixed title: localise it in the UI.</summary>
    public PlaylistKind Kind { get; set; }
    public VideoOwnerDto Owner { get; set; } = null!;

    /// <summary>Number of items the CURRENT viewer can see (hidden/unavailable videos are not counted).</summary>
    public int ItemCount { get; set; }

    /// <summary>Thumbnail of the first visible video.</summary>
    public string? ThumbnailUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Only set when the list was requested with ?videoId=... (for the "Save to playlist" dialog).</summary>
    public bool? ContainsVideo { get; set; }
}

public class PlaylistItemDto
{
    public Guid ItemId { get; set; }

    /// <summary>Sort key only: increasing, but not necessarily contiguous.</summary>
    public int Position { get; set; }
    public DateTime AddedAt { get; set; }
    public VideoListItemDto Video { get; set; } = null!;
}

public class PlaylistListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Public list: only playlists of this user (channel page).</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>"Mine" list: mark which playlists already contain this video.</summary>
    public Guid? VideoId { get; set; }
}
