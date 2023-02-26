using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using picoblog.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

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

  if (model.CoverImage.Contains(payload.Image) || model.Markdown?.Contains(payload.Image) == true) {
    if(!model.CoverImage.Contains(payload.Image))
      if (Config.Password != null && !User.Identity.IsAuthenticated)
        return NotFound();

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
    await HttpContext.Response.Body.WriteAsync(resize(file));
    return new EmptyResult();
  }

  private byte[] resize(byte[]? file) {
    using (var outputStream = new MemoryStream())
{
    using (var image = Image.Load(file))
    {
        int width = image.Width / 2;
        int height = image.Height / 2;
        image.Mutate(x =>x.Resize(width, height));
        image.SaveAsJpeg(outputStream);
    }

    var data = outputStream.ToArray();
    return data;
}
  }
}
