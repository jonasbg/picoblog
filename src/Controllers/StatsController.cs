namespace picoblog.Controllers;

public class StatsController : Controller
{
    private readonly ILogger<StatsController> _logger;
    private readonly VisitTracker _visitTracker;

    public StatsController(
        ILogger<StatsController> logger,
        VisitTracker visitTracker)
    {
        _logger = logger;
        _visitTracker = visitTracker;
    }

    [HttpGet]
    [Route("[Controller]")]
    public async Task<IActionResult> Index()
    {
        try
        {
            // Basic stats remain synchronous as they use in-memory Cache
            var monthlyActivity = Cache.Models
                .Where(x => x.Date.HasValue)
                .GroupBy(x => new { x.Date.Value.Year, x.Date.Value.Month })
                .Select(g => new MonthlyStats
                {
                    Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Await all async operations in parallel
            var uniqueVisitorsTask = _visitTracker.GetUniqueVisitorsPerMonthAsync();
            var mostLikedPostsTask = _visitTracker.GetTopPostsAsync("likes", 5);
            var mostViewedPostsTask = _visitTracker.GetTopPostsAsync("views", 5);
            var userAgentStatsTask = _visitTracker.GetUserAgentStatsAsync();

            await Task.WhenAll(
                uniqueVisitorsTask,
                mostLikedPostsTask,
                mostViewedPostsTask,
                userAgentStatsTask
            );

            var stats = new StatsViewModel
            {
                // Basic stats
                TotalPosts = Cache.Models.Count,
                PublicPosts = Cache.Models.Count(x => x.Public),
                PrivatePosts = Cache.Models.Count(x => !x.Public),
                PostsWithImages = Cache.Models.Count(x => !string.IsNullOrEmpty(x.CoverImage)),

                // Monthly activity and visitors
                MonthlyActivity = monthlyActivity,
                UniqueVisitors = await uniqueVisitorsTask,

                // Top posts
                MostLikedPosts = await mostLikedPostsTask,
                MostViewedPosts = await mostViewedPostsTask,

                // Browser stats
                UserAgentStats = await userAgentStatsTask
            };

            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating stats");

            // Return a basic version of stats if database operations fail
            return View(new StatsViewModel
            {
                TotalPosts = Cache.Models.Count,
                PublicPosts = Cache.Models.Count(x => x.Public),
                PrivatePosts = Cache.Models.Count(x => !x.Public),
                PostsWithImages = Cache.Models.Count(x => !string.IsNullOrEmpty(x.CoverImage)),
                MonthlyActivity = new List<MonthlyStats>(),
                UniqueVisitors = new List<MonthlyStats>(),
                MostLikedPosts = new List<TopPost>(),
                MostViewedPosts = new List<TopPost>(),
                UserAgentStats = new Dictionary<string, int>()
            });
        }
    }

    // Helper method to ensure consistent date ranges across charts
    private List<MonthlyStats> NormalizeDateRange(List<MonthlyStats> stats)
    {
        if (!stats.Any()) return stats;

        var minDate = stats.Min(s => s.Date);
        var maxDate = stats.Max(s => s.Date);
        var normalizedStats = new List<MonthlyStats>();

        for (var date = minDate; date <= maxDate; date = date.AddMonths(1))
        {
            var stat = stats.FirstOrDefault(s => s.Date.Year == date.Year && s.Date.Month == date.Month);
            normalizedStats.Add(new MonthlyStats
            {
                Date = date,
                Count = stat?.Count ?? 0
            });
        }

        return normalizedStats;
    }
}