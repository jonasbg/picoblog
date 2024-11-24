var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole(options =>
    {
        options.FormatterName = "Simple";
        options.TimestampFormat = "yyyy/MM/dd HH:mm:ss ";
    });

    // Set minimum log level
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
builder.WebHost.UseKestrel(option => option.AddServerHeader = false);
builder.Services.AddHealthChecks();

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
    _ = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.Cookie.Name = "Picoblog.AuthCookie";
            options.LoginPath = "/login";
        });
    _ = builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AuthorizeFilter());
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    });
    _ = builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Config.ConfigDir));
}
else
    _ = builder.Services.AddControllersWithViews(options =>
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

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (Config.Password != null)
{
    _ = app.UseCookiePolicy();
    _ = app.UseAuthentication();
    _ = app.UseAuthorization();
}

//app.UseWebOptimizer();
app.UseRequestLocalization(new RequestLocalizationOptions { ApplyCurrentCultureToResponseHeaders = true });
app.UseStaticFiles();
app.MapHealthChecks("/healthz");
app.UseRouting();

var supportedCultures = new[]
{
    new CultureInfo("nb-NO"),
    new CultureInfo("en-GB"),
};

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("nb-NO"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var serviceScope = app.Services.CreateScope())
{
    var services = serviceScope.ServiceProvider;
    var monitorLoop = services.GetRequiredService<MonitorLoop>();
    monitorLoop.StartMonitorLoop();
}

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started");

logger.LogTrace("Trace level log");
logger.LogDebug("Debug level log");
logger.LogInformation("Information level log");
logger.LogWarning("Warning level log");
logger.LogError("Error level log");
logger.LogCritical("Critical level log");

app.Run();
