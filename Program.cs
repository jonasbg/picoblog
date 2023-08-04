using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using picoblog.Models;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
// using SixLabors.ImageSharp.Web.DependencyInjection;
// using SixLabors.ImageSharp;
// using Microsoft.IO;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    // Add environment variable
    config.AddEnvironmentVariables();

    // Override logging level from environment variable
    var logLevel = Environment.GetEnvironmentVariable("PICOBLOG_LOG_LEVEL");
    if (!string.IsNullOrEmpty(logLevel))
    {
        config.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("Logging:LogLevel:Default", logLevel),
        });
    }
});


builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
builder.WebHost.UseKestrel(option => option.AddServerHeader = false);
builder.Services.AddHealthChecks();

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
       options.Cookie.HttpOnly = true; options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; options.Cookie.SameSite = SameSiteMode.Strict;
       options.ExpireTimeSpan = TimeSpan.FromDays(30);
       options.SlidingExpiration = true;
       options.Cookie.Name = "Picoblog.AuthCookie"; options.LoginPath = "/login";
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

var app = builder.Build();
// app.UseImageSharp();

if (Config.Password != null)
{
  app.UseCookiePolicy();
  app.UseAuthentication();
  app.UseAuthorization();
}

app.UseRequestLocalization(new RequestLocalizationOptions { ApplyCurrentCultureToResponseHeaders = true });
app.UseStaticFiles();
app.MapHealthChecks("/healthz");
app.UseRouting();

var supportedCultures = new[]
{
   new CultureInfo("nb-NO"),
   new CultureInfo("en-GB"),
};

app.UseRequestLocalization(new RequestLocalizationOptions{
    DefaultRequestCulture = new RequestCulture("nb-NO"),
    SupportedCultures=supportedCultures,
    SupportedUICultures=supportedCultures
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

app.Run();
