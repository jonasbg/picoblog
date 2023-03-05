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
    var onThisDay = Cache.Models.Where(p => p.Date?.Month == today.Month && p.Date?.Day == today.Day);
    var onThisDayWeek = Cache.Models
    .Where(
      p => p.Date?.Month == today.Month &&
      p.Date?.DayOfWeek == today.DayOfWeek &&
      p.Date <= today.AddDays(7) &&
      p.Date >= today.AddDays(-7));
    onThisDay.Concat(onThisDayWeek);
    return Ok(onThisDay);
  }
}