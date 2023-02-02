using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using picoblog.Models;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
builder.WebHost.UseKestrel(option => option.AddServerHeader = false);
builder.Services.AddHealthChecks();

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
}
else
  builder.Services.AddControllersWithViews(options =>
  {
      options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
  });

var app = builder.Build();

if (Config.Password != null)
{
  app.UseCookiePolicy();
  app.UseAuthentication();
  app.UseAuthorization();
}
app.UseStatusCodePagesWithReExecute("/Error/{0}");
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

Console.WriteLine("Starting searching for markdown files (*.md)");
var files = Directory.GetFiles(Config.DataDir, "*.md", SearchOption.AllDirectories);
foreach (var file in files)
{
  Console.Write($"Found file: {file} ");
  int counter = 0;
  var model = new MarkdownModel();

  foreach (string line in System.IO.File.ReadLines(file))
  {
    if (counter == 0 && !line.Trim().Equals("---"))
    {
      break;
    }
    if (counter > 0 && counter < 10)
    {
      if (line.StartsWith("public", StringComparison.InvariantCultureIgnoreCase))
      {
        if (line.Trim().EndsWith("true", StringComparison.InvariantCultureIgnoreCase))
        {
          model.Public = true;
          model.Path = file;
          Cache.Models.Add(model);
        }
      }
      if (line.Trim().StartsWith(MetadataHeader.Title, StringComparison.InvariantCultureIgnoreCase))
      {
        var title = line.Split(':')[1].Trim();
        model.Title = title;
      }
      if (line.Trim().StartsWith(MetadataHeader.Date, StringComparison.InvariantCultureIgnoreCase))
      {
        var date = line.Split(':')[1].Trim();
        model.Date = DateTime.Parse(date);
      }
      if (line.Trim().StartsWith(MetadataHeader.Hidden, StringComparison.InvariantCultureIgnoreCase))
      {
        var hidden = line.Split(':')[1].Trim().ToLower();
        model.Visible = hidden != "true";
      }
      if (line.Trim().StartsWith(MetadataHeader.Poster, StringComparison.InvariantCultureIgnoreCase))
      {
        var poster = line.Split(':')[1].Trim();
        model.Poster = $"{poster}";
      }
      if (line.Trim().StartsWith(MetadataHeader.Description, StringComparison.InvariantCultureIgnoreCase))
      {
        var description = line.Split(':')[1].Trim();
        model.Description = description;
      }

      if (line.Trim().Equals("---"))
        break;
    }
    if (counter >= 10)
      break;
    counter++;
  }
  if (Cache.Models.LastOrDefault() == model)
  {
    Console.Write("ADDED");
    if (model.Visible)
      Console.WriteLine($" ->  {Config.Domain}/post/{model.Title}");
    else
      Console.WriteLine();
  }

  if (Cache.Models.LastOrDefault() != model)
    Console.WriteLine("IGNORED");
}

if (Cache.Models.Any(p => p.Visible == false))
  Console.WriteLine($"FOUND {Cache.Models.Where(p => p.Visible == false).Count()} HIDDEN POSTS");
foreach (var model in Cache.Models.Where(p => p.Visible == false))
{
  Console.WriteLine($"{Config.Domain}/post/{model.Title}");
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
  exceptionHandlerApp.Run(async context =>
  {
    var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
    Console.WriteLine(exceptionHandlerPathFeature?.Error);
    context.Response.StatusCode = 500;
    await context.Response.WriteAsync("An exception was thrown.");
  });
});

app.Run();
