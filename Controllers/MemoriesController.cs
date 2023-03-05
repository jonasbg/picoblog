using Microsoft.AspNetCore.Mvc;
using picoblog.Models;

namespace picoblog.Controllers;

public class MemoriesController : Controller
{
  private readonly ILogger<MemoriesController> _logger;

  [HttpGet]
  [Route("[Controller]")]
  public IActionResult Index(){
    var today = DateTime.Now;
    var lower = today.AddDays(-7).Day > today.Day ? 0 : today.AddDays(-7).Day;
    var upper = today.AddDays(7).Day < today.Day ? 0 : today.AddDays(7).Day;
    var onThisDay = Cache.Models.Where(
      p => p.Date?.Month == today.Month &&
      p.Date?.Day <= upper &&
      p.Date?.Day >= lower);
    return PartialView("_index.content", onThisDay);
  }
}