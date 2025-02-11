using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class CustomLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    public CustomLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger =
            loggerFactory?.CreateLogger<CustomLoggingMiddleware>()
            ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    private string GetClientIp(HttpContext context)
    {
        // Try Cloudflare header first
        var cfConnectingIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(cfConnectingIp))
        {
            return cfConnectingIp;
        }

        // Try X-Forwarded-For
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        // Fallback to remote IP address
        var remoteIp = context.Connection?.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(remoteIp))
        {
            // Clean up IPv4 mapped to IPv6
            if (remoteIp.StartsWith("::ffff:"))
            {
                remoteIp = remoteIp.Substring(7);
            }
            return remoteIp;
        }

        return "unknown";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var startTime = DateTime.UtcNow;

        try
        {
            await _next(context);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            var durationMs = duration.TotalMilliseconds;

            // Skip health check endpoints
            if (!context.Request.Path.StartsWithSegments("/healthz"))
            {
                var clientIp = GetClientIp(context);
                var country = context.Request.Headers["CF-IPCountry"].FirstOrDefault() ?? "";

                var logMessage = new System.Text.StringBuilder()
                    .Append($"{DateTime.UtcNow:yyyy/MM/dd HH:mm:ss} ")
                    .Append($"method={context.Request.Method} ")
                    .Append($"path={context.Request.Path} ")
                    .Append($"status={context.Response.StatusCode} ")
                    .Append($"duration={durationMs:F4}ms ")
                    .Append($"ip={clientIp}");

                // Only add country if it exists
                if (!string.IsNullOrEmpty(country))
                {
                    logMessage.Append($" country={country}");
                }

                var message = logMessage.ToString();

                // Choose log level based on status code
                if (context.Response.StatusCode >= 500)
                {
                    _logger.LogError(message);
                }
                else if (context.Response.StatusCode >= 400)
                {
                    _logger.LogWarning(message);
                }
                else
                {
                    _logger.LogInformation(message);
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
