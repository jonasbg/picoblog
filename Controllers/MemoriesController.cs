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
    var onThisDay = Cache.Models.Where(
      p => p.Date?.Month == today.Month &&
      p.Date?.Day <= today.AddDays(7).Day &&
      p.Date?.Day >= today.AddDays(-7).Day);
    return Ok(onThisDay);
  }
}