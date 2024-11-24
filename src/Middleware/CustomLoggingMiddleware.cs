using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class CustomLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public CustomLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _logger = loggerFactory.CreateLogger<CustomLoggingMiddleware>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await _next(context);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            var durationMs = duration.TotalMilliseconds;

            // Golang-style log format
            var logMessage =
                $"{DateTime.UtcNow:yyyy/MM/dd HH:mm:ss} "
                + $"method={context.Request.Method} "
                + $"path={context.Request.Path} "
                + $"status={context.Response.StatusCode} "
                + $"duration={durationMs:F4}ms "
                + $"ip={context.Connection.RemoteIpAddress}";

            if (!$"{context.Request.Path}".Contains("/healthz"))
            {
                // Choose log level based on status code
                if (context.Response.StatusCode >= 500)
                {
                    _logger.LogError(logMessage);
                }
                else if (context.Response.StatusCode >= 400)
                {
                    _logger.LogWarning(logMessage);
                }
                else
                {
                    _logger.LogInformation(logMessage);
                }
            }
        }
    }
}

// Extension method to make registration cleaner
public static class CustomLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CustomLoggingMiddleware>();
    }
}
