using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using picoblog.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace picoblog.Controllers;

public class PostController : Controller
{
  private readonly ILogger<PostController> _logger;

  public PostController(ILogger<PostController> logger, MonitorLoop monitorLoop)
  {
    _logger = logger;
    monitorLoop.StartMonitorLoop();
  }

  [HttpGet]
  [Route("[Controller]/{title}/{**image}")]
  [Route("[Controller]/{title}")]
  [AllowAnonymous]
  public async Task<IActionResult> Index(Payload payload)
  {
    var model = Cache.Models.SingleOrDefault(f => f.Title == payload.Title);
    if(model == null)
      return NotFound();
    if (string.IsNullOrEmpty(payload.Image))
    {
        model.Markdown = System.IO.File.ReadAllText(model.Path);
        return View(model);
    }

  if (model.CoverImage.Contains(payload.Image) || model.Markdown?.Contains(payload.Image) == true) {
    if(!model.CoverImage.Contains(payload.Image))
      if (Config.Password != null && !User.Identity.IsAuthenticated)
        return NotFound();

      var path = $"{Path.GetDirectoryName(model.Path)}/{payload.Image}";
      return await Synology(path);
    }
    return NotFound();
  }

  private async Task<IActionResult> Synology(string path) {
    if (Config.Synology)
    {
      var synologyFile = Path.GetFileName(path);
      var directory = Path.GetDirectoryName(path);
      var synologyPath = $"@eaDir/{synologyFile}/{Config.SynologySize()}";
      synologyPath = $"{directory}/{synologyPath}";

      if (System.IO.File.Exists(synologyPath))
        path = synologyPath;
    }
    
    if (!System.IO.File.Exists(path)){
      if(path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".JPG", StringComparison.OrdinalIgnoreCase)){
        path = ToggleCaseExtension(path);  
        if (!System.IO.File.Exists(path)){
          return NotFound();
        }
      } else {
        return NotFound();
      }
    }
    
    HttpContext.Response.Headers.Add("ETag", ComputeMD5(path));
    HttpContext.Response.Headers.Add("Cache-Control", "private, max-age=12000");
    if(path.EndsWith("HEIC", StringComparison.OrdinalIgnoreCase)){
      HttpContext.Response.Headers.Add("Content-Type", "image/heic");
      var image = await System.IO.File.ReadAllBytesAsync(path);
      await HttpContext.Response.Body.WriteAsync(image);
    } else {
      await HttpContext.Response.Body.WriteAsync(await resize(path));
    }
    return new EmptyResult();
  }

  private string ToggleCaseExtension(string path)
  {
    string ext = System.IO.Path.GetExtension(path);
    string oppositeCaseExt = ext.Equals(ext.ToLower()) ? ext.ToUpper() : ext.ToLower();
    return System.IO.Path.ChangeExtension(path, oppositeCaseExt);
  }

  private string ComputeMD5(string s)
    {
        using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
        {
            return BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s)))
                        .Replace("-", "");
        }
    }

  private async Task<byte[]> resize(string path) {
    var fileName = $"{Config.ConfigDir}/images{path}";
    if (System.IO.File.Exists(fileName)) {
      using (var SourceStream = System.IO.File.Open(fileName, FileMode.Open))
      {
        var result = new byte[SourceStream.Length];
        await SourceStream.ReadAsync(result, 0, (int)SourceStream.Length);
        return result;
      }
    }

    using (var outputStream = new MemoryStream())
    {
      using (var image = await Image.LoadAsync(path))
      {
          int width = image.Width / 2;
          int height = image.Height / 2;
          width = 0;
          height = 0;
          if(image.Height > image.Width && height > Config.ImageMaxSize)
            height = Config.ImageMaxSize;
          if (image.Width > image.Height && width > Config.ImageMaxSize)
            width = Config.ImageMaxSize;

          if (width + height != 0)
            image.Mutate(x => x.Resize(width, height));
          JpegEncoder encoder = new JpegEncoder();
          encoder.Quality = Config.ImageQuality;
          await image.SaveAsJpegAsync(outputStream, encoder);
      }
      outputStream.Position = 0;
      Directory.CreateDirectory(Path.GetDirectoryName(fileName));
      using var destination = System.IO.File.Create(fileName, bufferSize: 4096);
      await outputStream.CopyToAsync(destination);

      return outputStream.ToArray();
    }
  }
}
