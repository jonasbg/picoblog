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

    public async Task<bool> AddLikeAsync(string postTitle, int year)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Likes (PostTitle, Year, LikedAt)
                VALUES (@title, @year, @time)";

            command.Parameters.AddWithValue("@title", postTitle);
            command.Parameters.AddWithValue("@year", year);
            command.Parameters.AddWithValue("@time", DateTime.UtcNow);

            await command.ExecuteNonQueryAsync();

            // Update Cache
            var model = Cache.Models.FirstOrDefault(m =>
                m.Title == postTitle && m.Date?.Year == year);

            if (model != null)
            {
                Interlocked.Increment(ref model.LikeCount);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding like for post {PostTitle}", postTitle);
            return false;
        }
    }

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
}