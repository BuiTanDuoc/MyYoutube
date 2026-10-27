using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniTube.Api.Extensions;
using MiniTube.Application.Common;
using MiniTube.Application.Users;
using MiniTube.Domain.Constants;

namespace MiniTube.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.Admin)]
public class AdminUsersController : ControllerBase
{
    private readonly IUserService _users;

    public AdminUsersController(IUserService users) => _users = users;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> List([FromQuery] UserListQuery query, CancellationToken ct) =>
        Ok(await _users.ListAsync(query, ct));

    [HttpPut("{id:guid}/lock")]
    public async Task<ActionResult<AdminUserDto>> SetLocked(Guid id, SetLockRequest request, CancellationToken ct) =>
        Ok(await _users.SetLockedAsync(User.GetUserId(), id, request.Locked,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    [HttpPut("{id:guid}/roles")]
    public async Task<ActionResult<AdminUserDto>> SetRoles(Guid id, SetRolesRequest request, CancellationToken ct) =>
        Ok(await _users.SetRolesAsync(User.GetUserId(), id, request.Roles, ct));
}
