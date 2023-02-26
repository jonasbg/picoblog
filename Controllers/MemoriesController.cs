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
    var memories = Cache.Models.Where(p => p.Date?.Month == today.Month && p.Date?.Day == today.Day);
    return Ok(memories);
  }
}