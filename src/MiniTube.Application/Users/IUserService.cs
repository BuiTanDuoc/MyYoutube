using MiniTube.Application.Common;

namespace MiniTube.Application.Users;

public interface IUserService
{
    Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task<PublicUserDto> GetPublicAsync(Guid userId, CancellationToken ct = default);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ip, CancellationToken ct = default);

    // Admin
    Task<PagedResult<AdminUserDto>> ListAsync(UserListQuery query, CancellationToken ct = default);
    Task<AdminUserDto> SetLockedAsync(Guid actingUserId, Guid targetUserId, bool locked, string? ip, CancellationToken ct = default);
    Task<AdminUserDto> SetRolesAsync(Guid actingUserId, Guid targetUserId, IReadOnlyCollection<string> roles, CancellationToken ct = default);
}
