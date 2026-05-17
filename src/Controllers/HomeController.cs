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
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Claims.Any())
            return RedirectToLocal(returnUrl);
        return View(new LoginViewModel { ReturnURL = NormalizeReturnUrl(returnUrl) });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (!ContentSecurity.PasswordEquals(model.Password, Config.Password))
        {
            string clientIp =
                HttpContext.Request.Headers["Cf-Connecting-Ip"].FirstOrDefault()
                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
            _logger.LogWarning("Failed login attempt by user with IP {IP}.", clientIp);
            return View(model);
        }

        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "shared password user") };
        var claimsIdentity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        var returnUrl = NormalizeReturnUrl(model.ReturnURL);

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
            return RedirectToLocal(returnUrl);
        }

        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        returnUrl = NormalizeReturnUrl(returnUrl);
        if (returnUrl != null)
        {
            return Redirect(returnUrl);
        }
        return Redirect("/");
    }

    private string? NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return null;

        return Url.IsLocalUrl(returnUrl) ? returnUrl : null;
    }

    [Route("")]
    public IActionResult Index()
    {
        ViewBag.Home = "class = active";
        return View(Cache.Models.OrderByDescending(f => f.Date));
    }

}
