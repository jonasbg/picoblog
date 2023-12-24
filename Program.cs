var builder = WebApplication.CreateBuilder(args);

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
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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
    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AuthorizeFilter());
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    });
    builder.Services.AddDataProtection()
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
builder.Services.AddHttpLogging(o => { });

var app = builder.Build();
app.UseHttpLogging();
// app.UseImageSharp();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (Config.Password != null)
{
    app.UseCookiePolicy();
    app.UseAuthentication();
    app.UseAuthorization();
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

app.Use(async (context, next) =>
{
    try
    {
        await next.Invoke();

        // If status code is not 200, log it
        if (context.Response.StatusCode != 200)
        {
            var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            Console.WriteLine($"[RemoteIP::{ip}]:[StatusCode::{context.Response.StatusCode}]:{context.Request.Path}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        // Re-throw the exception so it can be handled by other middleware
        throw;
    }
});

var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogTrace("Trace level log");
logger.LogDebug("Debug level log");
logger.LogInformation("Information level log");
logger.LogWarning("Warning level log");
logger.LogError("Error level log");
logger.LogCritical("Critical level log");

app.Run();
