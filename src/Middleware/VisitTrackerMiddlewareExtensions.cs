public static class VisitTrackerMiddlewareExtensions
{
    public static IApplicationBuilder UseVisitTracker(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<VisitTrackerMiddleware>();
    }

    public static IServiceCollection AddVisitTracker(
        this IServiceCollection services,
        string configDir
    )
    {
        services.AddSingleton<VisitTracker>(sp => new VisitTracker(
            configDir,
            sp.GetRequiredService<ILogger<VisitTracker>>()
        ));
        return services;
    }
}
