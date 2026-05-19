public class MonitorLoop : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(1);

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
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private DateTimeOffset _lastScan = DateTimeOffset.MinValue;
    private volatile bool _hasCompletedScan;
    private bool _disposed;

    public bool HasCompletedScan => _hasCompletedScan;

    public MonitorLoop(ILogger<MonitorLoop> logger, IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _applicationLifetime = applicationLifetime;
    }

    public void StartMonitorLoop()
    {
        _applicationLifetime.ApplicationStopping.Register(Dispose);
        _logger.LogInformation(
            "Markdown scans for {Directory} will run on demand at most every {IntervalMinutes} minutes",
            Config.DataDir,
            RefreshInterval.TotalMinutes
        );
        _ = Task.Run(RunInitialScanUntilComplete);
    }

    private async Task RunInitialScanUntilComplete()
    {
        var stopping = _applicationLifetime.ApplicationStopping;

        while (!stopping.IsCancellationRequested && !_hasCompletedScan)
        {
            RefreshIfStale();

            if (_hasCompletedScan)
                return;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stopping);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public void RefreshIfStale()
    {
        if (DateTimeOffset.UtcNow - _lastScan < RefreshInterval)
            return;

        if (!_scanLock.Wait(0))
        {
            _logger.LogInformation("Previous markdown scan still running; skipping refresh");
            return;
        }

        try
        {
            if (DateTimeOffset.UtcNow - _lastScan < RefreshInterval)
                return;

            LoadAllFiles();
            _lastScan = DateTimeOffset.UtcNow;
            _hasCompletedScan = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during markdown scan");
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private void LoadAllFiles()
    {
        _logger.LogInformation("Scanning markdown files in {Directory}", Config.DataDir);

        var models = new List<MarkdownModel>();
        var privatePosts = new List<MarkdownModel>();

        foreach (var file in EnumerateMarkdownFiles(Config.DataDir))
        {
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

    private IEnumerable<string> EnumerateMarkdownFiles(string root)
    {
        if (!Directory.Exists(root))
        {
            _logger.LogWarning("Data directory does not exist: {Directory}", root);
            yield break;
        }

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.TryPop(out var directory))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.md", options);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Skipping unreadable directory {Directory}", directory);
                continue;
            }

            foreach (var file in files)
                yield return file;

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(directory, "*", options);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Skipping unreadable subdirectories in {Directory}", directory);
                continue;
            }

            foreach (var child in directories)
            {
                if (IsExcludedDirectory(child))
                    continue;

                pending.Push(child);
            }
        }
    }

    private static bool IsExcludedDirectory(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return ExcludedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase);
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
        var model = new MarkdownModel { Path = file, Location = LocationParser.ParseLocation(frontmatter) };

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
                case var k
                    when k.Equals(
                            MetadataHeader.Password,
                            StringComparison.InvariantCultureIgnoreCase
                        )
                        || k.Equals(
                            MetadataHeader.Passphrase,
                            StringComparison.InvariantCultureIgnoreCase
                        ):
                    model.PostPassword = value;
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
        _scanLock.Dispose();
    }
}
