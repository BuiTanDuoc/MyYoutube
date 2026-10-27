using System.ComponentModel.DataAnnotations;

namespace MiniTube.Application.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> Roles { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

/// <summary>What anyone (even anonymous) may see about a user/channel owner.</summary>
public class PublicUserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public List<string> Roles { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class UpdateProfileRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Bio { get; set; }

    [MaxLength(500), Url]
    public string? AvatarUrl { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}

public class UserListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

public class SetLockRequest
{
    public bool Locked { get; set; }
}

public class SetRolesRequest
{
    [Required]
    public List<string> Roles { get; set; } = [];
}
