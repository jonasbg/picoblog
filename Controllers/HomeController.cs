namespace picoblog.Controllers;
[Route("")]
public class HomeController : Controller
{
  private readonly ILogger<HomeController> _logger;

  public HomeController(ILogger<HomeController> logger, MonitorLoop monitorLoop)
  {
    _logger = logger;
    monitorLoop.StartMonitorLoop();
  }

  [AllowAnonymous]
  [Route("/login")]
  public IActionResult Login()
  {
    if (User.Claims.Any())
      return Redirect("/");
    return View();
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
          string clientIp = HttpContext.Request.Headers["Cf-Connecting-Ip"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress.ToString();
          _logger.LogWarning("Failed login attempt by user with IP {IP}.", clientIp);
          return View(model);
      }

      var claims = new List<Claim>
      {
          new Claim(ClaimTypes.Name, "shared password user")
      };

      var claimsIdentity = new ClaimsIdentity(
          claims, CookieAuthenticationDefaults.AuthenticationScheme);
      var authProperties = new AuthenticationProperties() { IsPersistent = true };

      await HttpContext.SignInAsync(
          CookieAuthenticationDefaults.AuthenticationScheme,
          new ClaimsPrincipal(claimsIdentity),
          authProperties);

      if (!String.IsNullOrEmpty(model.ReturnURL) && Url.IsLocalUrl(model.ReturnURL) && IsValidReturnUrl(model.ReturnURL))
        return Redirect(model.ReturnURL);

      return RedirectToAction("/");
  }

  [Route("")]
  public IActionResult Index()
  {
    ViewBag.Home = "class = active";
    return View(Cache.Models.Where(p => p.Visible).OrderByDescending(f => f.Date));
  }

  private bool IsValidReturnUrl(string url)
  {
    var safeUrls = new List<string> { "/post", "/calendar", "/memories" };
    return safeUrls.Any(safeUrl => url.StartsWith(safeUrl, StringComparison.OrdinalIgnoreCase));
  }
}
