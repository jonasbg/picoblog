namespace picoblog.Controllers;

public class CalendarController : Controller
{
  private readonly ILogger<CalendarController> _logger;

  public CalendarController(ILogger<CalendarController> logger)
  {
    _logger = logger;
  }

  [HttpGet]
  [Route("[Controller]/all")]
  public async Task<IActionResult> All()
  {
    ViewBag.Calendar = "class = active";
    var models = Cache.Models.Where(p => p.Visible).Where(p => p.Date != null).OrderBy(p => p.Date).ToList();
    return View(models);
  }

  [HttpGet]
  [Route("[Controller]/{year}")]
  public async Task<IActionResult> Year(int? year)
  {
    ViewBag.Calendar = "class = active";
    var models = Cache.Models.Where(p => p.Visible).Where(f => f.Date?.Year == year).OrderBy(p => p.Date).ToList();
    var dictionary = new Dictionary<int, List<MarkdownModel>>();
    var months = models.Where(p => p.Date != null).Select(p => p.Date?.Month).Distinct().ToArray();
    foreach(int month in months)
      dictionary.Add(month, models.Where(p => p.Date?.Month == month).ToList());
    return View(dictionary);
  }

  [HttpGet]
  [Route("[Controller]")]
  public async Task<IActionResult> Index()
  {
    ViewBag.Calendar = "class = active";
    var models = Cache.Models.Where(p => p.Visible);
    var years = models.Select(p => p.Date?.Year).Distinct().OrderByDescending(p => p).ToList();
    return View(years);
  }
}
