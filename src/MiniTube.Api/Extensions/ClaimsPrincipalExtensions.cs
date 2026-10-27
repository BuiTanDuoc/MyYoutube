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

    /// <summary>Null for anonymous requests (endpoints that work with or without a token).</summary>
    public static Guid? TryGetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;
}
