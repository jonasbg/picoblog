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
  [Route("[Controller]/{year:int}/{title}/{**image}")]
  [Route("[Controller]/{year:int}/{title}")]
  [AllowAnonymous]
  public async Task<IActionResult> Index(Payload payload)
  {
    var model = Cache.Models.SingleOrDefault(f => f.Date?.Year == payload.Year && f.Title == payload.Title);
    if(model == null)
    {
      _logger.LogWarning("No model found for payload title: {PayloadTitle}", payload.Title);
      return NotFound();
    }

    if (string.IsNullOrEmpty(payload.Image))
    {
      _logger.LogDebug("Payload image is null or empty. Reading from model path: {ModelPath}", model.Path);
      model.Markdown = System.IO.File.ReadAllText(model.Path);
      return View(model);
    }

    if (model.CoverImage.Contains(payload.Image) || model.Markdown?.Contains(payload.Image) == true)
    {
      if(!model.CoverImage.Contains(payload.Image))
      {
        if (Config.Password != null && !User.Identity.IsAuthenticated)
        {
          _logger.LogWarning("Unauthenticated request with Config.Password set for image not as Cover");
          return Unauthorized();
        }
      }

      var path = $"{Path.GetDirectoryName(model.Path)}/{payload.Image}";
      _logger.LogDebug("Calling Synology method with path: {Path}", path);
      return await Synology(path);
    }
    else
    {
      _logger.LogWarning("Payload image not found in CoverImage and Markdown.");
      return NotFound();
    }
  }

private async Task<IActionResult> Synology(string path) {
    if (Config.Synology) {
        var synologyFile = Path.GetFileName(path);
        var directory = Path.GetDirectoryName(path);
        var synologyPath = $"{directory}/@eaDir/{synologyFile}/{Config.SynologySize()}";

        if (System.IO.File.Exists(synologyPath)) {
            path = synologyPath;
            _logger.LogDebug("Synology file exists. Updated path to: {0}", path);
        }
    }

    if (!System.IO.File.Exists(path))) {
        _logger.LogWarning("File does not exist at path: {0}", path);
        return NotFound();
    }

    path = await ResizeIfNeeded(path);

    HttpContext.Response.Headers.Add("ETag", ComputeMD5(path));
    HttpContext.Response.Headers.Add("Cache-Control", "private, max-age=12000");
    await HttpContext.Response.Body.WriteAsync(System.IO.File.ReadAllBytes(path));
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

  private async Task<string> ResizeIfNeeded(string path) {
      _logger.LogDebug("Checking if resize is needed for path: {0}", path);
      var resizedPath = $"{Config.ConfigDir}/resized{path}";

      if (System.IO.File.Exists(resizedPath))
          return resizedPath;

      try {
          using (var image = await Image.LoadAsync(path)) {
              int width, height;

              if (image.Height > image.Width) {
                  // Portrait or square
                  height = Math.Min(image.Height, Config.ImageMaxSize);
                  width = (image.Width * height) / image.Height;
              } else {
                  // Landscape
                  width = Math.Min(image.Width, Config.ImageMaxSize);
                  height = (image.Height * width) / image.Width;
              }

              if (image.Width != width || image.Height != height) {
                  image.Mutate(x => x.Resize(width, height));
                  await SaveResizedImage(image, resizedPath);
              }
          }
      } catch (Exception e) {
          _logger.LogError(e, "Error resizing file: {0}", path);
      }

      return resizedPath;
  }

  private async Task SaveResizedImage(Image image, string path) {
      JpegEncoder encoder = new JpegEncoder { Quality = Config.ImageQuality };
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      await image.SaveAsync(path, encoder);
  }

}
