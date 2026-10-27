using System.ComponentModel.DataAnnotations;

namespace MiniTube.Application.Search;

public enum SearchSort
{
    Relevance = 0,
    Newest = 1,
    MostViewed = 2,
    MostLiked = 3
}

public enum UploadDateFilter
{
    Any = 0,
    Today = 1,  // last 24 hours
    Week = 2,   // last 7 days
    Month = 3,  // last 30 days
    Year = 4    // last 365 days
}

public enum DurationFilter
{
    Any = 0,
    Short = 1,  // under 4 minutes
    Medium = 2, // 4 - 20 minutes
    Long = 3    // over 20 minutes
}

public class VideoSearchQuery
{
    [MaxLength(100)]
    public string? Q { get; set; }

    public SearchSort Sort { get; set; } = SearchSort.Relevance;
    public UploadDateFilter UploadDate { get; set; } = UploadDateFilter.Any;
    public DurationFilter Duration { get; set; } = DurationFilter.Any;

    /// <summary>Search inside one channel.</summary>
    public Guid? OwnerId { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ChannelSearchQuery
{
    [MaxLength(100)]
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SearchSuggestionDto
{
    /// <summary>"video" (a title) or "channel".</summary>
    public string Type { get; set; } = "video";
    public string Text { get; set; } = string.Empty;

    /// <summary>Channel id when Type = "channel".</summary>
    public Guid? Id { get; set; }
}

public class ChannelSearchResultDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int VideoCount { get; set; }
}

public class ReindexResultDto
{
    public int Videos { get; set; }
    public int Users { get; set; }
}
