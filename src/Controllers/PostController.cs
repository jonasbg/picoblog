namespace picoblog.Controllers;

public class PostController : Controller
{
    private readonly ILogger<PostController> _logger;
    private readonly GeocodingService _geocodingService;

    public PostController(ILogger<PostController> logger, GeocodingService geocodingService)
    {
        _logger = logger;
        _geocodingService = geocodingService;
    }

    [HttpGet]
    [Route("[Controller]/{year:int}/{title}/{**image}")]
    [Route("[Controller]/{year:int}/{title}")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(Payload payload)
    {
        var model = Cache.Models.SingleOrDefault(f =>
            f.Date?.Year == payload.Year && f.Title == payload.Title
        );
        if (model == null)
        {
            model = Cache.PrivatePosts.SingleOrDefault(f =>
                f.Date?.Year == payload.Year && f.Title == payload.Title
            );
            if (model == null)
            {
                _logger.LogWarning(
                    "No model found for payload title: {PayloadTitle}",
                    payload.Title
                );
                return NotFound();
            }
        }

        if (string.IsNullOrEmpty(payload.Image))
        {
            _logger.LogDebug(
                "Payload image is null or empty. Reading from model path: {ModelPath}",
                model.Path
            );

            try
            {
                model.Markdown = System.IO.File.ReadAllText(model.Path);
                if (model.Draft)
                    return RedirectBack(model);
                else
                {
                    if (Config.Password == null || User.Identity?.IsAuthenticated == true)
                        await _geocodingService.ResolveAsync(model.Location);

                    return View(model);
                }
            }
            catch (FileNotFoundException)
            {
                _logger.LogWarning(
                    "File not found at path: {ModelPath}. Removing from cache.",
                    model.Path
                );
                return RedirectBack(model);
            }
        }

        if (ContentSecurity.TryResolvePostImagePath(model, payload.Image, out var path, out var isCoverImage))
        {
            if (!isCoverImage)
            {
                if (Config.Password != null && User.Identity?.IsAuthenticated != true)
                {
                    _logger.LogWarning("Unauthenticated request with Config.Password set.");
                    return Unauthorized();
                }
            }

            _logger.LogDebug("Calling Synology method with path: {FilePath}", path);
            return await Synology(path);
        }
        else
        {
            _logger.LogWarning("Payload image not found in CoverImage and Markdown.");
            return NotFound();
        }
    }

    private IActionResult RedirectBack(MarkdownModel model)
    {
        if (Cache.Models.Contains(model))
            Cache.Models = Cache.Models.Where(m => m.Path != model.Path).ToList();
        else if (Cache.PrivatePosts.Contains(model))
            Cache.PrivatePosts = Cache.PrivatePosts.Where(m => m.Path != model.Path).ToList();

        // Redirect to previous page if available, otherwise to home
        var previousUrl = Request.Headers.Referer.ToString();
        if (!string.IsNullOrEmpty(previousUrl))
            return Redirect(previousUrl);

        return RedirectToAction("Index", "Home"); // Default fallback route
    }

    private async Task<IActionResult> Synology(string path)
    {
        if (Config.Synology)
        {
            var synologyFile = Path.GetFileName(path);
            var directory = Path.GetDirectoryName(path);
            var synologyPath = $"@eaDir/{synologyFile}/{Config.SynologySize()}";
            synologyPath = $"{directory}/{synologyPath}";

            if (System.IO.File.Exists(synologyPath))
            {
                path = synologyPath;
                _logger.LogDebug("Synology file exists. Updated path to: {FilePath}", path);
            }
        }

        if (!System.IO.File.Exists(path))
        {
            if (
                path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".JPG", StringComparison.OrdinalIgnoreCase)
            )
            {
                path = ToggleCaseExtension(path);
                if (!System.IO.File.Exists(path))
                {
                    _logger.LogWarning(
                        "File does not exist after toggling case of extension: {FilePath}",
                        path
                    );
                    return NotFound();
                }
            }
            else
            {
                _logger.LogWarning(
                    "File does not exist after toggling case of extension: {FilePath}",
                    path
                );
                return NotFound();
            }
        }

        var imageData = await resize(path);
        return File(imageData, "image/jpeg", Path.GetFileName(path));
    }

    private string ToggleCaseExtension(string path)
    {
        string ext = Path.GetExtension(path);
        string oppositeCaseExt = ext.Equals(ext.ToLower()) ? ext.ToUpper() : ext.ToLower();
        _logger.LogDebug(
            "Toggled case of extension. New path: {NewPath}, Old path: {OldPath}",
            oppositeCaseExt,
            path
        );
        return Path.ChangeExtension(path, oppositeCaseExt);
    }

    private async Task<byte[]> resize(string path)
    {
        _logger.LogDebug("Resize method started for path: {FilePath}", path);
        try
        {
            var fileName = ContentSecurity.CachePathForImage(path);
            if (System.IO.File.Exists(fileName))
            {
                return await System.IO.File.ReadAllBytesAsync(fileName);
            }

            using (var outputStream = new MemoryStream())
            {
                using (var image = await Image.LoadAsync(path))
                {
                    int width = image.Width / 2;
                    int height = image.Height / 2;
                    width = 0;
                    height = 0;
                    if (image.Height > image.Width && height > Config.ImageMaxSize)
                        height = Config.ImageMaxSize;
                    if (image.Width > image.Height && width > Config.ImageMaxSize)
                        width = Config.ImageMaxSize;

                    if (width + height != 0)
                        image.Mutate(x => x.Resize(width, height));
                    var encoder = new JpegEncoder { Quality = Config.ImageQuality };
                    await image.SaveAsJpegAsync(outputStream, encoder);
                }
                outputStream.Position = 0;
                var cacheDirectory = Path.GetDirectoryName(fileName);
                if (!string.IsNullOrEmpty(cacheDirectory))
                    Directory.CreateDirectory(cacheDirectory);
                using var destination = System.IO.File.Create(fileName, bufferSize: 4096);
                await outputStream.CopyToAsync(destination);

                return outputStream.ToArray();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error reading file: {FilePath}", path);
            throw;
        }
    }
}
