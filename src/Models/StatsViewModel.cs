public class StatsViewModel
{
    public int TotalPosts { get; set; }
    public int TotalUniqueVisitors { get; set; }  // New property
    public int TotalViews { get; set; }           // New property
    public List<MonthlyStats> MonthlyActivity { get; set; }
    public List<MonthlyStats> UniqueVisitors { get; set; }
    public List<TopPost> MostLikedPosts { get; set; }
    public List<TopPost> MostViewedPosts { get; set; }
    public Dictionary<string, int> UserAgentStats { get; set; }
}

public class MonthlyStats
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class TopPost
{
    public string Title { get; set; }
    public int Year { get; set; }
    public string CoverImage { get; set; }
    public int Count { get; set; }
}