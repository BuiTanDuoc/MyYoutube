using Microsoft.AspNetCore.Mvc;
using MiniTube.Application.Common;

namespace MiniTube.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            ProblemDetails problem = ex.Errors is { Count: > 0 }
                ? new ValidationProblemDetails(ex.Errors) { Status = ex.StatusCode, Title = ex.Message }
                : new ProblemDetails { Status = ex.StatusCode, Title = ex.Message };

            if (ex.Code is not null) problem.Extensions["code"] = ex.Code;

            await WriteAsync(context, problem);
        }
        catch (BadHttpRequestException ex)
        {
            // e.g. 413 when an upload exceeds [RequestSizeLimit]
            await WriteAsync(context, new ProblemDetails { Status = ex.StatusCode, Title = ex.Message });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499; // client closed request
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            });
        }
    }

    private static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.StatusCode = problem.Status ?? 500;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(problem, problem.GetType());
    }
}
