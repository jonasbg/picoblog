using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using picoblog.Models;

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
        return View(model);

      var claims = new List<Claim>
      {
          new Claim(nameof(LoginViewModel.Password), model.Password)
      };

      var claimsIdentity = new ClaimsIdentity(
          claims, CookieAuthenticationDefaults.AuthenticationScheme);
      var authProperties = new AuthenticationProperties() { IsPersistent = true };

      await HttpContext.SignInAsync(
          CookieAuthenticationDefaults.AuthenticationScheme,
          new ClaimsPrincipal(claimsIdentity),
          authProperties);

      if (!String.IsNullOrEmpty(model.ReturnURL) && model.ReturnURL.StartsWith("/"))
        return Redirect(model.ReturnURL);
      return RedirectToAction("/");
  }

  [Route("")]
  public IActionResult Index()
  {
    ViewBag.Home = "class = active";
    return View(Cache.Models.Where(p => p.Visible).OrderByDescending(f => f.Date));
  }
}
