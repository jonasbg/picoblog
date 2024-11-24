public class VisitTrackerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly VisitTracker _visitTracker;
    private readonly ILogger<VisitTrackerMiddleware> _logger;

    public VisitTrackerMiddleware(
        RequestDelegate next,
        VisitTracker visitTracker,
        ILogger<VisitTrackerMiddleware> logger)
    {
        _next = next;
        _visitTracker = visitTracker;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Don't delay the response - capture the task but don't await it
        var trackingTask = Task.CompletedTask;

        // Only track actual blog post views
        if (IsPostView(context.Request))
        {
            var visit = new VisitTracker.Visit
            {
                PostTitle = GetPostTitle(context.Request.Path),
                Year = GetPostYear(context.Request.Path),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                VisitTime = DateTime.UtcNow,
                Referrer = context.Request.Headers.Referer.ToString()
            };

            // Fire and forget tracking
            trackingTask = Task.Run(async () =>
            {
                try
                {
                    await _visitTracker.LogVisitAsync(visit);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error tracking visit");
                }
            });
        }

        // Continue the pipeline immediately
        await _next(context);

        // Optionally wait for tracking to complete after the response
        // Only if you want to ensure tracking completes before the request ends
        try
        {
            await trackingTask;
        }
        catch
        {
            // Suppress any tracking errors after response
        }
    }

    private bool IsPostView(HttpRequest request)
    {
        var path = request.Path.Value?.ToLower();
        return path?.StartsWith("/post/") == true &&
               !path.Contains(".jpg") &&
               !path.Contains(".png") &&
               !path.Contains(".gif") &&
               request.Method == "GET";
    }

    private string GetPostTitle(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments?.Length >= 3 ? segments[2] : "unknown";
    }

    private int GetPostYear(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments?.Length >= 2 && int.TryParse(segments[1], out var year) ? year : 0;
    }
}