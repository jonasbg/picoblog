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
    _logger.LogInformation("Index method started for payload: {Payload}", payload);
    
    var model = Cache.Models.SingleOrDefault(f => f.Title == payload.Title);
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
          _logger.LogWarning("Unauthenticated request with Config.Password set.");
          return NotFound();
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
    if (Config.Synology)
    {
      var synologyFile = Path.GetFileName(path);
      var directory = Path.GetDirectoryName(path);
      var synologyPath = $"@eaDir/{synologyFile}/{Config.SynologySize()}";
      synologyPath = $"{directory}/{synologyPath}";

      if (System.IO.File.Exists(synologyPath)) {
        path = synologyPath;
        _logger.LogDebug("Synology file exists. Updated path to: {0}", path);
      }
    }
    
    if (!System.IO.File.Exists(path)){
      if(path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".JPG", StringComparison.OrdinalIgnoreCase)){
        path = ToggleCaseExtension(path);  
        if (!System.IO.File.Exists(path)){
          _logger.LogWarning("File does not exist after toggling case of extension: {0}", path);
          return NotFound();
        }
      } else {
        _logger.LogWarning("File does not exist after toggling case of extension: {0}", path);
        return NotFound();
      }
    }
    
    HttpContext.Response.Headers.Add("ETag", ComputeMD5(path));
    HttpContext.Response.Headers.Add("Cache-Control", "private, max-age=12000");
    await HttpContext.Response.Body.WriteAsync(await resize(path));
    return new EmptyResult();
  }

  private string ToggleCaseExtension(string path)
  {
    string ext = System.IO.Path.GetExtension(path);
    string oppositeCaseExt = ext.Equals(ext.ToLower()) ? ext.ToUpper() : ext.ToLower();
    _logger.LogDebug("Toggled case of extension. New path: {0}\nOld path: {1}", oppositeCaseExt, path);
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
    _logger.LogDebug("Resize method started for path: {0}", path);
    try{
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
    } catch(Exception e){
      _logger.LogError(e, "Error Reading File: {0}", path);
      throw;
    }
  }
}
