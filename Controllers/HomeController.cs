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
            {
                return View("Index");
            }

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
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
            var authProperties = new AuthenticationProperties();

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
            return RedirectToAction("Index");
        }

  [Route("")]
  public IActionResult Index()
  {
    return View(Cache.Models.Where(p => p.Visible).OrderByDescending(f => f.Date));
  }

//   [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[Route("error")]
  public IActionResult Error()
  {
    var ctx = HttpContext;
    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
  }
}
