using Microsoft.AspNetCore.Mvc;
using System.Linq;
using picoblog.Models;

namespace picoblog.Controllers;

public class StatsController : Controller
{
    private readonly ILogger<StatsController> _logger;

    public StatsController(ILogger<StatsController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [Route("[Controller]")]
    public IActionResult Index()
    {
        var stats = new StatsViewModel
        {
            TotalPosts = Cache.Models.Count,
            PublicPosts = Cache.Models.Count(x => x.Public),
            PrivatePosts = Cache.Models.Count(x => !x.Public),
            PostsWithImages = Cache.Models.Count(x => !string.IsNullOrEmpty(x.CoverImage)),

            // Group posts by month and year
            MonthlyActivity = Cache.Models
                .Where(x => x.Date.HasValue)
                .GroupBy(x => new { x.Date.Value.Year, x.Date.Value.Month })
                .Select(g => new MonthlyStats
                {
                    Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                    PostCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList(),

            // Most active months
            MostActiveMonth = Cache.Models
                .Where(x => x.Date.HasValue)
                .GroupBy(x => new { x.Date.Value.Year, x.Date.Value.Month })
                .OrderByDescending(g => g.Count())
                .Select(g => new { Date = new DateTime(g.Key.Year, g.Key.Month, 1), Count = g.Count() })
                .FirstOrDefault(),

            // Average posts per month
            AveragePostsPerMonth = Cache.Models
                .Where(x => x.Date.HasValue)
                .GroupBy(x => new { x.Date.Value.Year, x.Date.Value.Month })
                .Average(g => g.Count()),

            // Longest and shortest posts
            LongestPost = Cache.Models
                .OrderByDescending(x => x.Markdown?.Length ?? 0)
                .FirstOrDefault(),

            ShortestPost = Cache.Models
                .Where(x => !string.IsNullOrEmpty(x.Markdown))
                .OrderBy(x => x.Markdown.Length)
                .FirstOrDefault(),

            // Post length distribution
            PostLengthDistribution = Cache.Models
                .Where(x => !string.IsNullOrEmpty(x.Markdown))
                .GroupBy(x => GetLengthCategory(x.Markdown.Length))
                .Select(g => new PostLengthStats
                {
                    Category = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Category)
                .ToList()
        };

        return View(stats);
    }

    private string GetLengthCategory(int length)
    {
        if (length < 500) return "Very Short (<500 chars)";
        if (length < 1500) return "Short (500-1500 chars)";
        if (length < 5000) return "Medium (1500-5000 chars)";
        if (length < 10000) return "Long (5000-10000 chars)";
        return "Very Long (>10000 chars)";
    }
}
