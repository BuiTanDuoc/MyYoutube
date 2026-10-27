namespace MiniTube.Application.Common;

public class AppException : Exception
{
    public int StatusCode { get; }
    public IDictionary<string, string[]>? Errors { get; }

    public AppException(int statusCode, string message, IDictionary<string, string[]>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
    }

    public static AppException BadRequest(string message, IDictionary<string, string[]>? errors = null) => new(400, message, errors);
    public static AppException Unauthorized(string message = "Unauthorized") => new(401, message);
    public static AppException Forbidden(string message = "Forbidden") => new(403, message);
    public static AppException NotFound(string message = "Not found") => new(404, message);
}
