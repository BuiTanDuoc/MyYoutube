using System.Security.Cryptography;
using System.Text;

namespace MiniTube.Api.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// Anonymous viewer identity for view de-duplication: a one-way hash of IP + user agent.
    /// The raw IP is never stored. (Behind a reverse proxy, enable ForwardedHeaders so this sees the real client IP.)
    /// </summary>
    public static string ComputeViewerKey(this HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var ua = context.Request.Headers.UserAgent.ToString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"minitube-view|{ip}|{ua}"));
        return Convert.ToHexString(hash)[..32];
    }
}
