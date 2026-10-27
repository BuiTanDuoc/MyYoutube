namespace MiniTube.Application.Common;

public class AppException : Exception
{
    public int StatusCode { get; }
    public IDictionary<string, string[]>? Errors { get; }

    /// <summary>Machine-readable code the frontend can switch on (e.g. "invalid_token").</summary>
    public string? Code { get; }

    public AppException(int statusCode, string message, IDictionary<string, string[]>? errors = null, string? code = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
        Code = code;
    }

    public static AppException BadRequest(string message, IDictionary<string, string[]>? errors = null, string? code = null) => new(400, message, errors, code);
    public static AppException Unauthorized(string message = "Unauthorized", string? code = null) => new(401, message, null, code);
    public static AppException Forbidden(string message = "Forbidden", string? code = null) => new(403, message, null, code);
    public static AppException NotFound(string message = "Not found") => new(404, message);
    public static AppException Conflict(string message, string? code = null) => new(409, message, null, code);
    public static AppException PayloadTooLarge(string message) => new(413, message);
}
