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
        var authProperties = new AuthenticationProperties() { IsPersistent = true };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties
        );

        // model.ReturnURL = HttpContext.Request.Query["returnUrl"].ToString() ?? "/";
        //model.ReturnURL is null why?

        return RedirectToLocal(model.ReturnURL ?? "/");
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
