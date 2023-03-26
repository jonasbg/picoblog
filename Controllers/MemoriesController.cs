using Microsoft.AspNetCore.Mvc;
using picoblog.Models;

namespace picoblog.Controllers;

public class MemoriesController : Controller
{
  private readonly ILogger<MemoriesController> _logger;

  [HttpPost]
  [IgnoreAntiforgeryToken]
  public IActionResult Index([FromBody] string[] json){
    if( json == null || !json.Any() )
      return new EmptyResult();
    var favorites = Cache.Models.Where(p => json.Contains(p.Title)).OrderByDescending(f => f.Date);
    return PartialView("_index.content", favorites);
  }

  [HttpGet]
  [Route("[Controller]")]
  public IActionResult Index()
  {
    var today = DateTime.Now;
    var lower = today.AddDays(-7);
    var upper = today.AddDays(7);
    var min = toInt(lower);
    var max = toInt(upper);
    var onThisDay = Cache.Models.Where(p =>
      toInt(p.Date) <= max &&
      toInt(p.Date) >= min &&
      p.Date?.Year != today.Year)
      .OrderByDescending(f => f.Date);
    return PartialView("_index.content", onThisDay);
  }

  private static int? toInt(DateTime? date){
    if (date != null)
     return int.Parse($"{date?.Month.ToString("00")}{date?.Day.ToString("00")}");
    return null;
    }
}