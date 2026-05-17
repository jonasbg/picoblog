var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole(options =>
    {
        options.FormatterName = "Simple";
        // options.TimestampFormat = "yyyy/MM/dd HH:mm:ss ";
    });

    // Set minimum log level
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
builder.WebHost.UseKestrel(option => option.AddServerHeader = false);
builder
    .Services.AddHealthChecks()
    .AddCheck<MarkdownScanHealthCheck>("markdown_scan", tags: new[] { "ready" });

builder.Services.AddHostedService<BackupService>();
builder.Services.AddSingleton<MonitorLoop>();
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddSingleton<IBackgroundTaskQueue>(ctx =>
{
    return new BackgroundTaskQueue(1);
});

// builder.Services.AddImageSharp(options => {
//   options.Configuration = Configuration.Default;
//   options.MemoryStreamManager = new RecyclableMemoryStreamManager();
//   options.BrowserMaxAge = TimeSpan.FromDays(7);
//   options.CacheMaxAge = TimeSpan.FromDays(365);
//   options.CacheHashLength = 8;
// });

if (Config.Password != null)
{
    builder
        .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.Cookie.Name = "Picoblog.AuthCookie";
            options.LoginPath = "/login";
        });
    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AuthorizeFilter());
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    });
    builder
        .Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(Config.ConfigDir));
}
else
    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    });

// builder.Services.AddWebOptimizer(pipeline =>
// {
//    pipeline.MinifyJsFiles("**/*.js");
//    pipeline.MinifyCssFiles("css/**/*.css");
// });

var app = builder.Build();
app.UseCustomLogging();

// app.UseImageSharp();

app.UseForwardedHeaders(
    new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    }
);

if (Config.Password != null)
{
    app.UseCookiePolicy();
    app.UseAuthentication();
    app.UseAuthorization();
}

//app.UseWebOptimizer();
app.UseStaticFiles();
app.MapHealthChecks(
    "/healthz",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false,
    }
);
app.MapHealthChecks(
    "/readyz",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    }
);
app.Use(async (context, next) =>
{
    if (
        !context.Request.Path.StartsWithSegments("/healthz")
        && !context.Request.Path.StartsWithSegments("/readyz")
    )
    {
        context.RequestServices.GetRequiredService<MonitorLoop>().RefreshIfStale();
    }

    await next();
});
app.UseRouting();

var supportedCultures = new[] { new CultureInfo("nb-NO"), new CultureInfo("en-GB") };

app.UseRequestLocalization(
    new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture("nb-NO"),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures,
        ApplyCurrentCultureToResponseHeaders = true,
    }
);

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

// app.MapGet("/debug-headers", async context =>
// {
//     var headers = context.Request.Headers
//         .Select(h => $"{h.Key}: {string.Join(", ", h.Value.ToArray())}")
//         .ToList();

//     await context.Response.WriteAsJsonAsync(new
//     {
//         RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
//         Headers = headers,
//         StandardProxyHeaders = new
//         {
//             XForwardedFor = context.Request.Headers["X-Forwarded-For"].ToString(),
//             XRealIP = context.Request.Headers["X-Real-IP"].ToString(),
//             XForwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString(),
//             XForwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString()
//         },
//         CloudflareHeaders = new
//         {
//             CFConnectingIP = context.Request.Headers["CF-Connecting-IP"].ToString(),
//             CFIPCountry = context.Request.Headers["CF-IPCountry"].ToString(),
//             CFIPCity = context.Request.Headers["CF-IPCity"].ToString(),
//             CFIPRegion = context.Request.Headers["CF-IPRegion"].ToString(),
//             CFRay = context.Request.Headers["CF-Ray"].ToString(),
//             CFVisitorScheme = context.Request.Headers["CF-Visitor"].ToString(),
//             CFWorker = context.Request.Headers["CF-Worker"].ToString(),
//             CFRequestPriority = context.Request.Headers["CF-Request-Priority"].ToString(),
//             CFAccessAuthenticatedUser = context.Request.Headers["CF-Access-Authenticated-User-Email"].ToString()
//         },
//         TrustChain = new
//         {
//             ForwardedForChain = context.Request.Headers["X-Forwarded-For"]
//                 .ToString()
//                 .Split(',')
//                 .Select(ip => ip.Trim())
//                 .ToArray() // Changed to ToArray() to make it explicit
//         }
//     });
// });

app.Services.GetRequiredService<MonitorLoop>().StartMonitorLoop();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started");

logger.LogTrace("Trace level log");
logger.LogDebug("Debug level log");
logger.LogInformation("Information level log");
logger.LogWarning("Warning level log");
logger.LogError("Error level log");
logger.LogCritical("Critical level log");

app.Run();
