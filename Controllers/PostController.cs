using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using picoblog.Models;

namespace picoblog.Controllers;

public class PostController : Controller
{
  private readonly ILogger<PostController> _logger;

  public PostController(ILogger<PostController> logger)
  {
    _logger = logger;
    // var locale = Request.HttpContext.Features.Get<IRequestCultureFeature>();
    // var BrowserCulture = locale.RequestCulture.UICulture.ToString();
    // Console.WriteLine($"Found this Culture {BrowserCulture}");
  }

  [ResponseCache(Duration = 1000, Location = ResponseCacheLocation.None, NoStore = true)]
  [HttpGet]
  [Route("[Controller]/{title}")]
  [AllowAnonymous]
  public IActionResult Index(string title)
  {
    var model = Cache.Models.FirstOrDefault(f => f.Title == title);

    if (model == null)
      return NotFound();
    // if(string.IsNullOrEmpty(model.Markdown))
    model.Markdown = System.IO.File.ReadAllText(model.Path);
    return View(model);
  }
}