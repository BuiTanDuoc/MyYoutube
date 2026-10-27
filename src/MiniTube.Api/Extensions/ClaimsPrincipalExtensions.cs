using System.Security.Claims;

namespace MiniTube.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing or invalid subject claim.");
    }
}
