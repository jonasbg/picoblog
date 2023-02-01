using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Authorization;
using picoblog.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

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
  });
}
else
  builder.Services.AddControllersWithViews();

var app = builder.Build();

if (Config.Password != null)
{
  app.UseCookiePolicy();
  app.UseAuthentication();
  app.UseAuthorization();
}
app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
app.UseRequestLocalization(new RequestLocalizationOptions
{
  ApplyCurrentCultureToResponseHeaders = true
});
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Use(async (context, next) =>
{
  if (context.Request.Method=="TRACE")
  {
      context.Response.StatusCode = 405;
      return;
  }
  var currentEndpoint = context.GetEndpoint();
  if (context.Request.Path.Value == "/")
  {
    await next(context);
    return;
  }
  var path = context.Request.Path.Value;

  if (!Cache.Models.Any(p => p.PosterPath == path))
  {
    path = path?.Replace("/post/", "").Replace($"{Config.DataDir}/", "").Replace("/home/", "");
    if (!Cache.Models.Any(p => p.Markdown != null && path != null && p.Markdown.Contains(path)))
    {
      await next(context);
      return;
    }
  }

  context.Request.Headers.TryGetValue("Accept", out var headerAccepts);

  if (headerAccepts.Any(f => f.Contains("image/")))
  {
    context.Request.Headers.TryGetValue("Referer", out var headerValue);

    path = path.Replace("/post/", "").Replace($"{Config.DataDir}/", "").Replace("/home/", "");

    if (headerValue.Any(p => p.Contains("post")))
    {
      if (!File.Exists($"{Config.DataDir}/{path}"))
      {
        var title = headerValue[0].Substring(headerValue[0].IndexOf("post"));
        title = System.Net.WebUtility.UrlDecode(title).Replace("post/", "");
        var directory = Cache.Models.First(p => p.Title == title).Path;
        path = $"{Path.GetDirectoryName(directory)}/{path}";
      }
      else
      {
        path = $"{Config.DataDir}/{path}";
      }
    }
    else
    {
      path = $"{Config.DataDir}/{path}";
    }

    if (!File.Exists(path))
    {
      await next(context);
      return;
    }

    Byte[]? file = null;
    if (Config.Synology)
    {
      var synologyFile = Path.GetFileName(path);
      var synologyPath = $"@eaDir/{synologyFile}/{Config.SynologySize()}";
      synologyPath = $"{Path.GetDirectoryName(path)}/{synologyPath}";

      if (System.IO.File.Exists(synologyPath))
        file = System.IO.File.ReadAllBytes($"{synologyPath}");
    }

    if (file is null)
      file = System.IO.File.ReadAllBytes($"{path}");

    await context.Response.BodyWriter.WriteAsync(file);
    return;
  }
  await next(context);
});

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
        var postPath = line.Split(':')[1].Trim();
        model.PosterPath = $"{Path.GetDirectoryName(file)}/{postPath}";
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
