using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using picoblog.Models;

namespace picoblog.Controllers;

public class HomeController : Controller
{
  private readonly ILogger<HomeController> _logger;

  public HomeController(ILogger<HomeController> logger)
  {
    _logger = logger;
  }

  [Route("")]
  [Route("Home")]
  [Route("Home/Index")]
  public IActionResult Index()
  {
    return View(Cache.Models.Where(p => p.Visible).OrderByDescending(f => f.Date));
  }

//   [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
  public IActionResult Error()
  {
    var ctx = HttpContext;
    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
  }
}
