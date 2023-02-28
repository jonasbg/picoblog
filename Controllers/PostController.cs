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
  private readonly IMemoryCache _memoryCache;

  public PostController(ILogger<PostController> logger, IMemoryCache memoryCache)
  {
    _logger = logger;
    _memoryCache = memoryCache;
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
    Byte[]? file = null;
    if (Config.Synology)
    {
      var synologyFile = Path.GetFileName(path);
      var directory = Path.GetDirectoryName(path);
      var synologyPath = $"@eaDir/{synologyFile}/{Config.SynologySize()}";
      synologyPath = $"{directory}/{synologyPath}";

      if (System.IO.File.Exists(synologyPath))
        path = synologyPath;
    }
    if (!System.IO.File.Exists(path))
      return NotFound();
    HttpContext.Response.Headers.Add("ETag", ComputeMD5(path));
    HttpContext.Response.Headers.Add("Cache-Control", "private, max-age=12000");
    await HttpContext.Response.Body.WriteAsync(await resize(path));
    return new EmptyResult();
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
    if (_memoryCache.TryGetValue(path, out byte[] cacheValue))
      return cacheValue;

    var cacheEntryOptions = new MemoryCacheEntryOptions()
          .SetSlidingExpiration(TimeSpan.FromMinutes(Config.CacheTimeInMinutes));

    using (var outputStream = new MemoryStream())
    {
      using (var image = await Image.LoadAsync(path))
      {
          int width = image.Width / 2;
          int height = image.Height / 2;
          width = 0;
          height = Config.ImageMaxHeight;
          image.Mutate(x => x.Resize(width, height));
          JpegEncoder encoder = new JpegEncoder();
          encoder.Quality = Config.ImageQuality;
          image.SaveAsJpeg(outputStream, encoder);
      }

    var data = outputStream.ToArray();
    _memoryCache.Set(path, data, cacheEntryOptions);
    return data;
    }
  }
}
