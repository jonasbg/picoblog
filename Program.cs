using Microsoft.AspNetCore.StaticFiles;
using picoblog.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
app.UseRequestLocalization();
// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "post",
    pattern: "{controller=Post}/{action=Index}/{title}");

app.Use(async (context, next) =>
{
    var currentEndpoint = context.GetEndpoint();
    if (context.Request.Path.Value == "/")
    {
        await next(context);
        return;
    }

    context.Request.Headers.TryGetValue("Accept", out var headerAccepts);

    if(headerAccepts.Any(f => f.Contains("image/"))) {
        context.Request.Headers.TryGetValue("Referer", out var headerValue);
        var path = context.Request.Path.Value;

        path = path.Replace("/post/", "").Replace($"{Config.DataDir}/", "").Replace("/home/", "");

        if(headerValue.Any(p => p.Contains("post")))
        {
            if(!File.Exists($"{Config.DataDir}/{path}"))
            {
                var title = headerValue[0].Substring(headerValue[0].IndexOf("post"));
                title = System.Net.WebUtility.UrlDecode(title).Replace("post/", "");
                var directory = Cache.Models.First(p => p.Title == title).Path;
                path = $"{Path.GetDirectoryName(directory)}/{path}";
            } else {
                path = $"{Config.DataDir}/{path}";
            }
        } else {
            path = $"{Config.DataDir}/{path}";
        }

        if(!File.Exists(path))
        {
            await next(context);
            return;
        }

        // var provider = new FileExtensionContentTypeProvider();
        // provider.TryGetContentType(path, out string contentType);

        // if(contentType?.StartsWith("image/") != false)
        //     return;

        Byte[]? file = null;
        if(Config.Synology){
          var synologyFile = Path.GetFileName(path);
          var synologyPath = $"@eaDir/{synologyFile}/SYNOPHOTO_THUMB_XL.jpg";
          synologyPath = $"{Path.GetDirectoryName(path)}/{synologyPath}";

          if (System.IO.File.Exists(synologyPath))
            file = System.IO.File.ReadAllBytes($"{synologyPath}");
        }

        if(file is null)
          file = System.IO.File.ReadAllBytes($"{path}");

        await context.Response.BodyWriter.WriteAsync(file);
        return;
    }

    await next(context);
});

    var files = Directory.GetFiles(Config.DataDir, "*.md", SearchOption.AllDirectories);
    Console.WriteLine("Starting searching for markdown files (*.md)");
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
      if(Cache.Models.LastOrDefault() == model){
        Console.Write("ADDED");
        Console.WriteLine($" ->  {Config.Domain}/post/{model.Title}");
      }

    if(Cache.Models.LastOrDefault() != model)
      Console.WriteLine("IGNORED");
    }

    if(Cache.Models.Any(p => p.Visible == false))
      Console.WriteLine($"FOUND {Cache.Models.Where(p => p.Visible == false).Count()} HIDDEN POSTS");
    foreach(var model in Cache.Models.Where(p => p.Visible == false)){
      Console.WriteLine($"{Config.Domain}/post/{model.Title}");
    }
app.Run();
