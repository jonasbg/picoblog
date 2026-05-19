namespace picoblog.Controllers;

public class PostController : Controller
{
    private readonly ILogger<PostController> _logger;
    private readonly GeocodingService _geocodingService;
    private readonly IDataProtector _postAccessProtector;

    public PostController(
        ILogger<PostController> logger,
        GeocodingService geocodingService,
        IDataProtectionProvider dataProtectionProvider
    )
    {
        _logger = logger;
        _geocodingService = geocodingService;
        _postAccessProtector = dataProtectionProvider.CreateProtector("picoblog.post-access");
    }

    [HttpGet]
    [Route("[Controller]/{year:int}/{title}/{**image}")]
    [Route("[Controller]/{year:int}/{title}")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(Payload payload)
    {
        var model = FindPost(payload);
        if (model == null)
        {
            _logger.LogWarning("No model found for payload title: {PayloadTitle}", payload.Title);
            return NotFound();
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
                    var canViewPost = CanViewPost(model);
                    ViewData["CanViewPost"] = canViewPost;

                    if (canViewPost)
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

        model.Markdown ??= System.IO.File.ReadAllText(model.Path);
        if (ContentSecurity.TryResolvePostImagePath(model, payload.Image, out var path, out var isCoverImage))
        {
            if (!isCoverImage)
            {
                if (!CanViewPost(model))
                {
                    _logger.LogWarning("Unauthorized request for locked post image.");
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

    [HttpPost]
    [Route("[Controller]/{year:int}/{title}")]
    [AllowAnonymous]
    [EnableRateLimiting("password-attempts")]
    public IActionResult Unlock(Payload payload, [FromForm] string password)
    {
        var model = FindPost(payload);
        if (model == null)
        {
            _logger.LogWarning("No model found for payload title: {PayloadTitle}", payload.Title);
            return NotFound();
        }

        model.Markdown = System.IO.File.ReadAllText(model.Path);
        if (model.Draft)
            return RedirectBack(model);

        if (!model.HasPostPassword)
            return RedirectToAction(nameof(Index), new { year = payload.Year, title = payload.Title });

        if (!ContentSecurity.PasswordEquals(password, model.PostPassword))
        {
            string clientIp =
                HttpContext.Request.Headers["Cf-Connecting-Ip"].FirstOrDefault()
                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
            _logger.LogWarning(
                "Failed post unlock attempt for {PostTitle} by user with IP {IP}.",
                model.Title,
                clientIp
            );
            ViewData["CanViewPost"] = false;
            ViewData["PostPasswordError"] = "Wrong password.";
            return View("Index", model);
        }

        Response.Cookies.Append(
            PostAccessCookieName(model),
            CreatePostAccessCookieValue(model),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
            }
        );

        return RedirectToAction(nameof(Index), new { year = payload.Year, title = payload.Title });
    }

    private MarkdownModel? FindPost(Payload payload)
    {
        var title = NormalizePostTitle(payload.Title);
        return Cache.Models.SingleOrDefault(f =>
                f.Date?.Year == payload.Year
                && string.Equals(f.Title, title, StringComparison.InvariantCultureIgnoreCase)
            )
            ?? Cache.PrivatePosts.SingleOrDefault(f =>
                f.Date?.Year == payload.Year
                && string.Equals(f.Title, title, StringComparison.InvariantCultureIgnoreCase)
            );
    }

    private static string? NormalizePostTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        return Uri.UnescapeDataString(title).TrimEnd('/');
    }

    private bool CanViewPost(MarkdownModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return true;

        if (model.HasPostPassword)
            return HasPostAccess(model);

        return Config.Password == null;
    }

    private bool HasPostAccess(MarkdownModel model)
    {
        if (!Request.Cookies.TryGetValue(PostAccessCookieName(model), out var cookieValue))
            return false;

        try
        {
            var unprotected = _postAccessProtector.Unprotect(cookieValue);
            return ContentSecurity.PasswordEquals(unprotected, PostAccessToken(model));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private string CreatePostAccessCookieValue(MarkdownModel model)
    {
        return _postAccessProtector.Protect(PostAccessToken(model));
    }

    private static string PostAccessCookieName(MarkdownModel model)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PostAccessKey(model))))
            .ToLowerInvariant();
        return $"Picoblog.PostAccess.{hash}";
    }

    private static string PostAccessToken(MarkdownModel model)
    {
        var passwordHash = Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(model.PostPassword ?? string.Empty)))
            .ToLowerInvariant();
        return $"{PostAccessKey(model)}:{passwordHash}";
    }

    private static string PostAccessKey(MarkdownModel model)
    {
        return $"{model.Date?.Year}:{model.Title}";
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
