namespace picoblog.Controllers;

[Route("")]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [AllowAnonymous]
    [Route("/login")]
    public IActionResult Login(string returnUrl = null)
    {
        if (User.Claims.Any())
            return Redirect("/");
        return View(new LoginViewModel { ReturnURL = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (!model.Password.Equals(Config.Password))
        {
            string clientIp =
                HttpContext.Request.Headers["Cf-Connecting-Ip"].FirstOrDefault()
                ?? HttpContext.Connection.RemoteIpAddress.ToString();
            _logger.LogWarning("Failed login attempt by user with IP {IP}.", clientIp);
            return View(model);
        }

        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "shared password user") };
        var claimsIdentity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        // Encode only the last segment of the return URL
        var returnUrl = model.ReturnURL;
        if (!string.IsNullOrEmpty(returnUrl))
        {
            var segments = returnUrl.Split('/');
            if (segments.Length > 0)
            {
                segments[segments.Length - 1] = Uri.EscapeDataString(segments[segments.Length - 1]);
                returnUrl = string.Join("/", segments);
            }
            model.ReturnURL = returnUrl;
        }

        try
        {
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = true }
            );
        }
        catch (Exception)
        {
            return RedirectToLocal(returnUrl ?? "/");
        }

        return RedirectToLocal(returnUrl ?? "/");
    }

    private IActionResult RedirectToLocal(string returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl) && !string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return Redirect("/");
    }

    [Route("")]
    public IActionResult Index()
    {
        ViewBag.Home = "class = active";
        return View(Cache.Models.OrderByDescending(f => f.Date));
    }

    private bool IsValidReturnUrl(string url)
    {
        var safeUrls = new List<string> { "/post", "/calendar", "/memories" };
        return safeUrls.Any(safeUrl => url.StartsWith(safeUrl, StringComparison.OrdinalIgnoreCase));
    }
}
