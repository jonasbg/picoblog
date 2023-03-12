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
  public IActionResult Index(){
    var today = DateTime.Now;
    var lower = today.AddDays(-7).Day > today.Day ? 0 : today.AddDays(-7).Day;
    var upper = today.AddDays(7).Day < today.Day ? 0 : today.AddDays(7).Day;
    var onThisDay = Cache.Models.Where(
      p => p.Date?.Month == today.Month &&
      p.Date?.Day <= upper &&
      p.Date?.Day >= lower &&
      p.Date.Year != today.year)
      .OrderByDescending(f => f.Date);
    return PartialView("_index.content", onThisDay);
  }
}