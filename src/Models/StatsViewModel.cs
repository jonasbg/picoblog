public class StatsViewModel
{
    public int TotalPosts { get; set; }
    public int PublicPosts { get; set; }
    public int PrivatePosts { get; set; }
    public int PostsWithImages { get; set; }
    public List<MonthlyStats> MonthlyActivity { get; set; }
    public dynamic MostActiveMonth { get; set; }
    public double AveragePostsPerMonth { get; set; }
    public MarkdownModel LongestPost { get; set; }
    public MarkdownModel ShortestPost { get; set; }
    public List<PostLengthStats> PostLengthDistribution { get; set; }
}

public class MonthlyStats
{
    public DateTime Date { get; set; }
    public int PostCount { get; set; }
}

public class PostLengthStats
{
    public string Category { get; set; }
    public int Count { get; set; }
}