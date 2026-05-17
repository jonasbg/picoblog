public class MonitorLoop : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private static readonly string[] ExcludedDirectories =
    {
        "@eaDir",
        "#recycle",
        ".DS_Store",
        "@Recycle",
        "$RECYCLE.BIN",
        "System Volume Information",
    };

    private readonly ILogger<MonitorLoop> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private Timer? _timer;
    private bool _disposed;

    public MonitorLoop(ILogger<MonitorLoop> logger, IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _applicationLifetime = applicationLifetime;
    }

    public void StartMonitorLoop()
    {
        if (_timer != null)
            return;

        _applicationLifetime.ApplicationStopping.Register(Dispose);
        _logger.LogInformation(
            "Starting polling-based markdown scan for {Directory} every {IntervalMinutes} minutes",
            Config.DataDir,
            PollInterval.TotalMinutes
        );
        _timer = new Timer(PollForChanges, null, TimeSpan.Zero, PollInterval);
    }

    private void PollForChanges(object? state)
    {
        if (!_pollLock.Wait(0))
        {
            _logger.LogInformation("Previous markdown scan still running; skipping this poll");
            return;
        }

        try
        {
            LoadAllFiles();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during markdown polling");
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private void LoadAllFiles()
    {
        _logger.LogInformation("Scanning markdown files in {Directory}", Config.DataDir);

        var models = new List<MarkdownModel>();
        var privatePosts = new List<MarkdownModel>();
        var files = Directory.EnumerateFiles(Config.DataDir, "*.md", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            if (IsExcludedPath(file))
                continue;

            try
            {
                var model = ProcessFile(file);
                if (model == null)
                    continue;

                if (model.Public && !model.Draft)
                    models.Add(model);
                else if (!model.Public && !model.Draft)
                    privatePosts.Add(model);
                else if (model.Draft)
                    _logger.LogDebug("Ignoring draft post at {Path}", model.Path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process markdown file {Path}", file);
            }
        }

        UpdateCaches(models, privatePosts);
    }

    private static bool IsExcludedPath(string path)
    {
        return ExcludedDirectories.Any(dir =>
            path.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar)
            || path.EndsWith(Path.DirectorySeparatorChar + dir)
        );
    }

    private static MarkdownModel? ProcessFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var match = Regex.Match(content, @"^---\r?\n(.*?)\r?\n---", RegexOptions.Singleline);

        if (!match.Success)
            return null;

        return ProcessFrontMatter(match.Groups[1].Value, filePath);
    }

    private static MarkdownModel ProcessFrontMatter(string frontmatter, string file)
    {
        var model = new MarkdownModel { Path = file };

        foreach (var line in frontmatter.Split('\n'))
        {
            string[] parts = line.Split(':', 2);
            if (parts.Length < 2)
                continue;

            string key = parts[0].Trim();
            string value = parts[1].Trim();

            switch (key.ToLowerInvariant())
            {
                case var k
                    when k.Equals(
                        MetadataHeader.Public,
                        StringComparison.InvariantCultureIgnoreCase
                    ):
                    model.Public = value.Equals(
                        "true",
                        StringComparison.InvariantCultureIgnoreCase
                    );
                    break;
                case var k
                    when k.Equals(
                        MetadataHeader.Title,
                        StringComparison.InvariantCultureIgnoreCase
                    ):
                    model.Title = value;
                    break;
                case var k
                    when k.Equals(MetadataHeader.Date, StringComparison.InvariantCultureIgnoreCase):
                    if (DateTime.TryParse(value, out var date))
                        model.Date = date;
                    break;
                case var k
                    when k.Equals(
                        MetadataHeader.Draft,
                        StringComparison.InvariantCultureIgnoreCase
                    ):
                    model.Draft = value.Equals("true", StringComparison.InvariantCultureIgnoreCase);
                    break;
                case var k
                    when k.Equals(
                        MetadataHeader.CoverImage,
                        StringComparison.InvariantCultureIgnoreCase
                    ):
                    model.CoverImage = value;
                    break;
                case var k
                    when k.Equals(
                        MetadataHeader.Description,
                        StringComparison.InvariantCultureIgnoreCase
                    ):
                    model.Description = value;
                    break;
            }
        }

        return model;
    }

    private void UpdateCaches(List<MarkdownModel> models, List<MarkdownModel> privatePosts)
    {
        var postsWithoutTitles = models.Where(p => string.IsNullOrEmpty(p.Title)).ToList();
        if (postsWithoutTitles.Any())
        {
            _logger.LogInformation("Found {Count} posts without titles", postsWithoutTitles.Count);
            models = models.Where(p => !string.IsNullOrEmpty(p.Title)).ToList();
        }

        var duplicates = models
            .GroupBy(p => new { p.Title, p.Date?.Year })
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Skip(1))
            .ToList();

        if (duplicates.Any())
        {
            _logger.LogInformation("Found {Count} duplicate posts; keeping first match", duplicates.Count);
            models = models.Except(duplicates).ToList();
        }

        Cache.Models = models;
        Cache.PrivatePosts = privatePosts;

        _logger.LogInformation(
            "Markdown scan complete: {PublicCount} public posts, {PrivateCount} private posts",
            Cache.Models.Count,
            Cache.PrivatePosts.Count
        );
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer?.Dispose();
        _pollLock.Dispose();
    }
}
