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
  }

  [HttpGet]
  [Route("[Controller]/{title}/{**image}")]
  [Route("[Controller]/{title}")]
  [AllowAnonymous]
  public async Task<IActionResult> Index(Payload payload)
  {
    var model = Cache.Models.SingleOrDefault(f => f.Title == payload.Title);
    if(model == null)
      return NotFound();
    if (string.IsNullOrEmpty(payload.Image))
    {
        model.Markdown = System.IO.File.ReadAllText(model.Path);
        return View(model);
    }

    if (model.Poster.Contains(payload.Image) || model.Markdown?.Contains(payload.Image) == true)
    {
      var path = $"{Path.GetDirectoryName(model.Path)}/{payload.Image}";
      return await Synology(path);
    }
    return NotFound();
  }

  private async Task<IActionResult> Synology(string path) {
    Byte[]? file = null;
    if (Config.Synology)
    {
      var synologyFile = Path.GetFileName(path);
      var directory = Path.GetDirectoryName(path);
      var synologyPath = $"@eaDir/{synologyFile}/{Config.SynologySize()}";
      synologyPath = $"{directory}/{synologyPath}";

      if (System.IO.File.Exists(synologyPath))
        file = await System.IO.File.ReadAllBytesAsync($"{synologyPath}");
    } else
      file = await System.IO.File.ReadAllBytesAsync(path);
    if (!System.IO.File.Exists(path))
      return NotFound();
    await HttpContext.Response.Body.WriteAsync(file);
    return new EmptyResult();
  }
}
