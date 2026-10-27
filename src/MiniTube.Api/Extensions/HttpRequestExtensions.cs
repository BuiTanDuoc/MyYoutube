namespace MiniTube.Api.Extensions;

public static class HttpRequestExtensions
{
    /// <summary>
    /// "en" when the client sends Accept-Language: en..., otherwise "vi".
    /// The React app should send the language currently selected in its language switcher.
    /// </summary>
    public static string GetLanguage(this HttpRequest request)
    {
        var header = request.Headers.AcceptLanguage.ToString();
        return header.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
    }
}
