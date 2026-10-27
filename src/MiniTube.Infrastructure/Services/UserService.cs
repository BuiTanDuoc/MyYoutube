using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniTube.Application.Common;
using MiniTube.Application.Users;
using MiniTube.Domain.Constants;
using MiniTube.Domain.Entities;
using MiniTube.Infrastructure.Persistence;

namespace MiniTube.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;

    public UserService(UserManager<ApplicationUser> users, AppDbContext db)
    {
        _users = users;
        _db = db;
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await FindOrThrowAsync(userId);
        return user.ToDto(await _users.GetRolesAsync(user));
    }

    public async Task<PublicUserDto> GetPublicAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw AppException.NotFound("User not found");
        return user.ToPublicDto();
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await FindOrThrowAsync(userId);

        user.DisplayName = request.DisplayName.Trim();
        user.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        (await _users.UpdateAsync(user)).ThrowIfFailed();
        return user.ToDto(await _users.GetRolesAsync(user));
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ip, CancellationToken ct = default)
    {
        var user = await FindOrThrowAsync(userId);

        (await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword))
            .ThrowIfFailed("Could not change password");

        // Invalidate every refresh token: other devices must log in again
        DateTime? now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedByIp, ip), ct);
    }

    // ---------------- Admin ----------------

    public async Task<PagedResult<AdminUserDto>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var q = _users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(u => u.Email!.Contains(s) || u.UserName!.Contains(s) || u.DisplayName.Contains(s));
        }

        var total = await q.CountAsync(ct);
        var users = await q.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

        var ids = users.Select(u => u.Id).ToList();
        var roleRows = await (from ur in _db.UserRoles
                              join r in _db.Roles on ur.RoleId equals r.Id
                              where ids.Contains(ur.UserId)
                              select new { ur.UserId, RoleName = r.Name! }).ToListAsync(ct);
        var roleMap = roleRows.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

        return new PagedResult<AdminUserDto>
        {
            Items = users.Select(u => u.ToAdminDto(roleMap.GetValueOrDefault(u.Id) ?? [])).ToList(),
            Page = page,
            PageSize = size,
            TotalCount = total
        };
    }

    public async Task<AdminUserDto> SetLockedAsync(Guid actingUserId, Guid targetUserId, bool locked, string? ip, CancellationToken ct = default)
    {
        if (actingUserId == targetUserId)
            throw AppException.BadRequest("You cannot lock your own account.");

        var user = await FindOrThrowAsync(targetUserId);

        (await _users.SetLockoutEnabledAsync(user, true)).ThrowIfFailed();
        (await _users.SetLockoutEndDateAsync(user, locked ? DateTimeOffset.MaxValue : null)).ThrowIfFailed();

        if (locked)
        {
            DateTime? now = DateTime.UtcNow;
            await _db.RefreshTokens
                .Where(t => t.UserId == targetUserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedByIp, ip), ct);
        }

        return user.ToAdminDto(await _users.GetRolesAsync(user));
    }

    public async Task<AdminUserDto> SetRolesAsync(Guid actingUserId, Guid targetUserId, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        var wanted = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var invalid = wanted.Where(r => !Roles.All.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        if (invalid.Count > 0)
            throw AppException.BadRequest("Unknown role(s): " + string.Join(", ", invalid));

        // Normalise casing and always keep the base "User" role
        wanted = Roles.All.Where(r => wanted.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        if (!wanted.Contains(Roles.User)) wanted.Add(Roles.User);

        if (actingUserId == targetUserId && !wanted.Contains(Roles.Admin))
            throw AppException.BadRequest("You cannot remove your own Admin role.");

        var user = await FindOrThrowAsync(targetUserId);
        var current = await _users.GetRolesAsync(user);

        var toRemove = current.Except(wanted).ToList();
        var toAdd = wanted.Except(current).ToList();

        if (toRemove.Count > 0) (await _users.RemoveFromRolesAsync(user, toRemove)).ThrowIfFailed();
        if (toAdd.Count > 0) (await _users.AddToRolesAsync(user, toAdd)).ThrowIfFailed();

        return user.ToAdminDto(await _users.GetRolesAsync(user));
    }

    private async Task<ApplicationUser> FindOrThrowAsync(Guid id) =>
        await _users.FindByIdAsync(id.ToString()) ?? throw AppException.NotFound("User not found");
}
