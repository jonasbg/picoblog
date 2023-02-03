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
  [Route("[Controller]/{title}/{path}/{image}")]
  [AllowAnonymous]
  public async Task<IActionResult> Index(string title, string subPath, string image)
  {
    var model = Cache.Models.FirstOrDefault(p => p.Title == title);
    if (model == null)
      {
        var referer = Request.Headers["Referer"];
        if (!referer.Any())
        {
          Console.WriteLine($"{title}/{subPath}/{image} from [{Request.Headers["Referer"]}] not found");
          return NotFound();
        }

        var refererTitle = referer[0].Substring(referer[0].IndexOf("post"));
        if(subPath == null)
          subPath = title;
        title = System.Net.WebUtility.UrlDecode(refererTitle).Replace("post/", "");
        model = Cache.Models.FirstOrDefault(p => p.Title == title);
        image = $"{subPath}/{image}";
      }
    if(model.Poster.Contains(image) && (model.Markdown != null && !model.Markdown.Contains(image)))
    {
      Console.WriteLine($"{title}/{subPath}/{image} from [{Request.Headers["Referer"]}] not found in model.Poster og markdowns");
      return NotFound();
    }
    var directory = model.Path;
    string imagePath;
    if(subPath == null)
      imagePath = $"{Path.GetDirectoryName(directory)}/{image}";
    else
      imagePath = $"{Path.GetDirectoryName(directory)}/{subPath}/{image}";
    if(model.Poster.Contains(image))
      imagePath = $"{Path.GetDirectoryName(directory)}/{model.Poster}";
    if (!System.IO.File.Exists(imagePath))
    {
      Console.WriteLine($"{title}/{subPath}/{image} from [{Request.Headers["Referer"]}] not found as file");
      return NotFound();
    }
    var imageFile = await Synology(imagePath);
    HttpContext.Response.Body.WriteAsync(imageFile);
    return new EmptyResult();
  }

  [HttpGet]
  [Route("[Controller]/{title}/{image}")]
  [AllowAnonymous]
  public async Task<IActionResult> Index(string title, string image)
  {
    var model = Cache.Models.FirstOrDefault(p => p.Title == title);
    if (model == null)
      {
        var referer = Request.Headers["Referer"];
        if (!referer.Any())
        {
          Console.WriteLine($"{title}/{image} from [{referer}] not found");
          return NotFound();
        }

        var refererTitle = referer[0].Substring(referer[0].IndexOf("post"));
        var subPath = title;
        title = System.Net.WebUtility.UrlDecode(refererTitle).Replace("post/", "");
        model = Cache.Models.FirstOrDefault(p => p.Title == title);
        image = $"{subPath}/{image}";
      }
    if(model.Poster != image && !model.Markdown.Contains(image))
    {
      var referer = Request.Headers["Referer"];
      Console.WriteLine($"{title}/{image} from [{referer}] not found in model.Poster og markdowns");
      return NotFound();
    }
    var directory = model.Path;
    var imagePath = $"{Path.GetDirectoryName(directory)}/{image}";
    if (!System.IO.File.Exists(imagePath))
    {
      var referer = Request.Headers["Referer"];
      Console.WriteLine($"{title}/{image} from [{referer}] not found as file");
      return NotFound();
    }
    var imageFile = await Synology(imagePath);
    // HttpContext.Response.Body.WriteAsync(imageFile);
    return File(imageFile, "image/jpeg");
  }

  // [ResponseCache(Duration = 1000, Location = ResponseCacheLocation.None, NoStore = true)]
  [HttpGet]
  [Route("[Controller]/{title}")]
  [AllowAnonymous]
  public async Task<IActionResult> Index(Post post)
  {
    var model = Cache.Models.FirstOrDefault(f => f.Title == post.Title);

    if (model == null){
      var referer = Request.Headers["Referer"];
      var refererTitle = referer[0].Substring(referer[0].IndexOf("post"));
      refererTitle = System.Net.WebUtility.UrlDecode(refererTitle).Replace("post/", "");
      var directory = Cache.Models.First(p => p.Title == refererTitle).Path;

      var imagePath = $"{Path.GetDirectoryName(directory)}/{post.Title}";

    if (!Cache.Models.Any(p => p.Markdown != null && post.Title != null && p.Markdown.Contains(post.Title)))
    {
      Console.WriteLine($"{post.Title} from [{referer}] not found as file");
      return NotFound();
    }
      if (!System.IO.File.Exists(imagePath))
    {
      Console.WriteLine($"{post.Title} from [{referer}] not found as file");
      return NotFound();
    }
      var image = await Synology(imagePath);
      HttpContext.Response.Body.WriteAsync(image);
      return new EmptyResult();
    }
    model.Markdown = System.IO.File.ReadAllText(model.Path);
    return View(model);
  }

  public async Task<Byte[]> Synology(string path) {
    Byte[]? file = null;
    if (Config.Synology)
    {
      var synologyFile = Path.GetFileName(path);
      var directory = Path.GetDirectoryName(path);
      var synologyPath = $"@eaDir/{synologyFile}/{Config.SynologySize()}";
      synologyPath = $"{directory}/{synologyPath}";

      if (System.IO.File.Exists(synologyPath))
        return await System.IO.File.ReadAllBytesAsync($"{synologyPath}");
    }

    return await System.IO.File.ReadAllBytesAsync($"{path}");
  }
}
