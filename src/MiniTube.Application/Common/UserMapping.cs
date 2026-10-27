using MiniTube.Application.Users;
using MiniTube.Domain.Entities;

namespace MiniTube.Application.Common;

public static class UserMapping
{
    public static UserDto ToDto(this ApplicationUser u, IEnumerable<string> roles) => new()
    {
        Id = u.Id,
        UserName = u.UserName ?? string.Empty,
        Email = u.Email ?? string.Empty,
        DisplayName = u.DisplayName,
        Bio = u.Bio,
        AvatarUrl = u.AvatarUrl,
        Roles = roles.ToList(),
        CreatedAt = u.CreatedAt
    };

    public static PublicUserDto ToPublicDto(this ApplicationUser u) => new()
    {
        Id = u.Id,
        UserName = u.UserName ?? string.Empty,
        DisplayName = u.DisplayName,
        Bio = u.Bio,
        AvatarUrl = u.AvatarUrl,
        CreatedAt = u.CreatedAt
    };

    public static AdminUserDto ToAdminDto(this ApplicationUser u, IEnumerable<string> roles) => new()
    {
        Id = u.Id,
        UserName = u.UserName ?? string.Empty,
        Email = u.Email ?? string.Empty,
        DisplayName = u.DisplayName,
        EmailConfirmed = u.EmailConfirmed,
        IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
        LockoutEnd = u.LockoutEnd,
        Roles = roles.ToList(),
        CreatedAt = u.CreatedAt
    };
}
