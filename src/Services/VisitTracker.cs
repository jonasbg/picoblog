using Microsoft.Data.Sqlite;

public class VisitTracker
{
    public class Visit
    {
        public string PostTitle { get; set; }
        public int Year { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public DateTime VisitTime { get; set; }
        public string Referrer { get; set; }
    }

    private readonly string _dbPath;
    private readonly ILogger<VisitTracker> _logger;

    public VisitTracker(string configDir, ILogger<VisitTracker> logger)
    {
        _dbPath = Path.Combine(configDir, "visits.db");
        _logger = logger;
        InitializeDatabase();
        UpdateCacheViewCounts();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Visits (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PostTitle TEXT NOT NULL,
                Year INTEGER NOT NULL,
                IpAddress TEXT,
                UserAgent TEXT,
                VisitTime DATETIME NOT NULL,
                Referrer TEXT
            );
            CREATE INDEX IF NOT EXISTS IX_Visits_PostTitle ON Visits(PostTitle);
            CREATE INDEX IF NOT EXISTS IX_Visits_VisitTime ON Visits(VisitTime);

            CREATE TABLE IF NOT EXISTS Likes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PostTitle TEXT NOT NULL,
                Year INTEGER NOT NULL,
                LikedAt DATETIME NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Likes_PostTitle ON Likes(PostTitle);";

        command.ExecuteNonQuery();
    }

    private async Task<bool> UpdateLikeAsync(string postTitle, int year, bool isAdd)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = isAdd
                ? @"INSERT INTO Likes (PostTitle, Year, LikedAt) VALUES (@title, @year, @time)"
                : @"DELETE FROM Likes WHERE PostTitle = @title AND Year = @year";

            command.Parameters.AddWithValue("@title", postTitle);
            command.Parameters.AddWithValue("@year", year);

            if (isAdd)
            {
                command.Parameters.AddWithValue("@time", DateTime.UtcNow);
            }

            await command.ExecuteNonQueryAsync();

            // Update Cache
            var model = Cache.Models.FirstOrDefault(m =>
                m.Title == postTitle && m.Date?.Year == year);

            if (model != null)
            {
                if (isAdd)
                    Interlocked.Increment(ref model.LikeCount);
                else
                    Interlocked.Decrement(ref model.LikeCount);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error {Action} like for post {PostTitle}",
                isAdd ? "adding" : "removing", postTitle);
            return false;
        }
    }

    public Task<bool> AddLikeAsync(string postTitle, int year) =>
        UpdateLikeAsync(postTitle, year, true);

    public Task<bool> RemoveLikeAsync(string postTitle, int year) =>
        UpdateLikeAsync(postTitle, year, false);

    private void UpdateCacheViewCounts()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            // Get view counts
            var viewCommand = connection.CreateCommand();
            viewCommand.CommandText = @"
                SELECT PostTitle, Year, COUNT(*) as ViewCount
                FROM Visits
                GROUP BY PostTitle, Year";

            using (var reader = viewCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var postTitle = reader.GetString(0);
                    var year = reader.GetInt32(1);
                    var viewCount = reader.GetInt32(2);

                    var model = Cache.Models.FirstOrDefault(m =>
                        m.Title == postTitle && m.Date?.Year == year);

                    if (model != null)
                    {
                        model.ViewCount = viewCount;
                    }
                }
            }

            // Get like counts
            var likeCommand = connection.CreateCommand();
            likeCommand.CommandText = @"
                SELECT PostTitle, Year, COUNT(*) as LikeCount
                FROM Likes
                GROUP BY PostTitle, Year";

            using (var reader = likeCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var postTitle = reader.GetString(0);
                    var year = reader.GetInt32(1);
                    var likeCount = reader.GetInt32(2);

                    var model = Cache.Models.FirstOrDefault(m =>
                        m.Title == postTitle && m.Date?.Year == year);

                    if (model != null)
                    {
                        model.LikeCount = likeCount;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cache counts");
        }
    }

    public async Task LogVisitAsync(Visit visit)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Visits (PostTitle, Year, IpAddress, UserAgent, VisitTime, Referrer)
                VALUES (@title, @year, @ip, @agent, @time, @referrer)";

            command.Parameters.AddWithValue("@title", visit.PostTitle);
            command.Parameters.AddWithValue("@year", visit.Year);
            command.Parameters.AddWithValue("@ip", visit.IpAddress);
            command.Parameters.AddWithValue("@agent", visit.UserAgent);
            command.Parameters.AddWithValue("@time", visit.VisitTime);
            command.Parameters.AddWithValue("@referrer", visit.Referrer ?? "");

            await command.ExecuteNonQueryAsync();

            // Update Cache
            var model = Cache.Models.FirstOrDefault(m =>
                m.Title == visit.PostTitle && m.Date?.Year == visit.Year);

            if (model != null)
            {
                Interlocked.Increment(ref model.ViewCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging visit for post {PostTitle}", visit.PostTitle);
        }
    }

    public async Task<Dictionary<string, int>> GetViewCountsAsync()
    {
        var viewCounts = new Dictionary<string, int>();

        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT PostTitle, COUNT(*) as ViewCount
                FROM Visits
                GROUP BY PostTitle";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                viewCounts[reader.GetString(0)] = reader.GetInt32(1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving view counts");
        }

        return viewCounts;
    }
    public async Task<List<MonthlyStats>> GetUniqueVisitorsPerMonthAsync()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT
                strftime('%Y-%m', VisitTime) as Month,
                COUNT(DISTINCT IpAddress) as UniqueVisitors
            FROM Visits
            GROUP BY strftime('%Y-%m', VisitTime)
            ORDER BY Month";

            var stats = new List<MonthlyStats>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var monthStr = reader.GetString(0);
                stats.Add(new MonthlyStats
                {
                    Date = DateTime.ParseExact(monthStr + "-01", "yyyy-MM-dd", null),
                    Count = reader.GetInt32(1)
                });
            }
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unique visitors per month");
            return new List<MonthlyStats>();
        }
    }

    public async Task<Dictionary<string, int>> GetUserAgentStatsAsync()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT
                CASE
                    WHEN UserAgent LIKE '%Mobile%' THEN 'Mobile'
                    WHEN UserAgent LIKE '%Chrome%' THEN 'Chrome'
                    WHEN UserAgent LIKE '%Firefox%' THEN 'Firefox'
                    WHEN UserAgent LIKE '%Safari%' THEN 'Safari'
                    WHEN UserAgent LIKE '%Edge%' THEN 'Edge'
                    ELSE 'Other'
                END as Browser,
                COUNT(*) as Count
            FROM Visits
            GROUP BY Browser
            ORDER BY Count DESC";

            var stats = new Dictionary<string, int>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                stats[reader.GetString(0)] = reader.GetInt32(1);
            }
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user agent stats");
            return new Dictionary<string, int>();
        }
    }

    public async Task<List<TopPost>> GetTopPostsAsync(string metric = "views", int limit = 5)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var tableName = metric.ToLower() == "likes" ? "Likes" : "Visits";
            var command = connection.CreateCommand();
            command.CommandText = $@"
            SELECT
                PostTitle,
                Year,
                COUNT(*) as Count
            FROM {tableName}
            GROUP BY PostTitle, Year
            ORDER BY Count DESC
            LIMIT @limit";

            command.Parameters.AddWithValue("@limit", limit);

            var topPosts = new List<TopPost>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var title = reader.GetString(0);
                var year = reader.GetInt32(1);
                var model = Cache.Models.FirstOrDefault(m =>
                    m.Title == title && m.Date?.Year == year);

                if (model != null)
                {
                    topPosts.Add(new TopPost
                    {
                        Title = title,
                        Year = year,
                        CoverImage = model.CoverImage,
                        Count = reader.GetInt32(2)
                    });
                }
            }
            return topPosts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top posts");
            return new List<TopPost>();
        }
    }
}