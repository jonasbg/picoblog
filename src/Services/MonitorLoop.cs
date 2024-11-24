public class MonitorLoop
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);
    private DateTime _lastSync = DateTime.MinValue;
    private static readonly string[] SupportedExtensions = { "*.md" };

    public MonitorLoop(
        IBackgroundTaskQueue taskQueue,
        ILogger<MonitorLoop> logger,
        IHostApplicationLifetime applicationLifetime
    )
    {
        _taskQueue = taskQueue;
        _logger = logger;
        _cancellationToken = applicationLifetime.ApplicationStopping;
    }

    public void StartMonitorLoop()
    {
        // Run a console user input loop in a background thread
        Task.Run(async () => await MonitorAsync());
    }

    private async ValueTask MonitorAsync()
    {
        await _taskQueue.QueueBackgroundWorkItemAsync(BuildWorkItem);
    }

    private async ValueTask BuildWorkItem(CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        var guid = Guid.NewGuid().ToString();

        try
        {
            if (ShouldSync())
            {
                _logger.LogInformation("Queued Background Task {Guid} is starting.", guid);
                await ProcessMarkdownFilesAsync(token);
                _lastSync = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            // Prevent throwing if cancelled
        }
    }

    private bool ShouldSync() =>
        _lastSync == DateTime.MinValue || DateTime.UtcNow - _lastSync >= _syncInterval;

    private async Task ProcessMarkdownFilesAsync(CancellationToken token)
    {
        _logger.LogInformation("Starting searching for markdown files");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var models = await FindAndProcessFilesAsync(token);
            if (token.IsCancellationRequested) return;

            var processor = new ResultsProcessor(_logger);
            processor.ProcessResults(models, Cache.Models);
            Cache.Models = models;

            stopwatch.Stop();
            _logger.LogInformation("Total processing time: {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing markdown files");
            throw;
        }
    }

    private async Task<List<MarkdownModel>> FindAndProcessFilesAsync(CancellationToken token)
    {
        var files = new List<string>();
        foreach (var extension in SupportedExtensions)
        {
            files.AddRange(Directory.EnumerateFiles(Config.DataDir, extension, SearchOption.AllDirectories));
        }

        var models = new ConcurrentBag<MarkdownModel>();
        var tasks = files.Select(file => ProcessFileAsync(file, models, token));
        await Task.WhenAll(tasks);

        return models.ToList();
    }

    private async Task ProcessFileAsync(string file, ConcurrentBag<MarkdownModel> models, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        try
        {
            string content = await File.ReadAllTextAsync(file, token);
            var frontMatter = ExtractFrontMatter(content);
            if (frontMatter != null)
            {
                var model = ParseFrontMatter(frontMatter, file);
                if (model != null)
                {
                    _logger.LogInformation(
                        "FOUND: {Title} - Path: {Path} - URL: {Url}",
                        model.Title,
                        model.Path,
                        $"{Config.Domain}/post/{model.Title}"
                    );
                    models.Add(model);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {File}", file);
        }
    }

    private static string ExtractFrontMatter(string content)
    {
        var match = Regex.Match(content, @"^---\n(.*?)\n---", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static MarkdownModel ParseFrontMatter(string frontMatter, string file)
    {
        var model = new MarkdownModel { Path = file };
        var frontMatterLines = frontMatter.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in frontMatterLines)
        {
            var parts = line.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            switch (key.ToLowerInvariant())
            {
                case var k when k.Equals(MetadataHeader.Public.ToLowerInvariant()):
                    model.Public = bool.Parse(value);
                    break;
                case var k when k.Equals(MetadataHeader.Title.ToLowerInvariant()):
                    model.Title = value;
                    break;
                case var k when k.Equals(MetadataHeader.Date.ToLowerInvariant()):
                    model.Date = DateTime.Parse(value);
                    break;
                case var k when k.Equals(MetadataHeader.Draft.ToLowerInvariant()):
                    model.Visible = !bool.Parse(value);
                    break;
                case var k when k.Equals(MetadataHeader.CoverImage.ToLowerInvariant()):
                    model.CoverImage = value;
                    break;
                case var k when k.Equals(MetadataHeader.Description.ToLowerInvariant()):
                    model.Description = value;
                    break;
            }
        }

        return model;
    }
}

public class ResultsProcessor
{
    private readonly ILogger _logger;
    private readonly Stopwatch _stopwatch;

    public ResultsProcessor(ILogger logger)
    {
        _logger = logger;
        _stopwatch = new Stopwatch();
    }

    public void ProcessResults(IList<MarkdownModel> currentModels, IList<MarkdownModel> cachedModels)
    {
        ProcessHiddenPosts(currentModels);
        RemovePostsWithoutTitles(currentModels);
        HandleDuplicates(currentModels);
        ProcessDeletedFiles(currentModels, cachedModels);
    }

    private void ProcessHiddenPosts(IEnumerable<MarkdownModel> models)
    {
        _stopwatch.Restart();
        var hiddenPosts = models.Where(p => !p.Visible).ToList();

        if (hiddenPosts.Any())
        {
            _logger.LogInformation("Found {Count} hidden posts", hiddenPosts.Count);
            foreach (var model in hiddenPosts)
            {
                _logger.LogInformation(
                    "Hidden post: {Title} - {Url}",
                    model.Title,
                    $"{Config.Domain}/post/{model.Date?.Year}/{model.Title}"
                );
            }
        }

        LogProcessingTime("Hidden posts");
    }

    private void RemovePostsWithoutTitles(IList<MarkdownModel> models)
    {
        _stopwatch.Restart();
        var postsWithoutTitles = models.Where(p => string.IsNullOrEmpty(p.Title)).ToList();

        if (postsWithoutTitles.Any())
        {
            _logger.LogInformation("Found {Count} posts without titles", postsWithoutTitles.Count);
            foreach (var model in postsWithoutTitles)
            {
                _logger.LogInformation("Post without title: {Path}", model.Path);
            }
            foreach (var model in postsWithoutTitles)
            {
                models.Remove(model);
            }
        }

        LogProcessingTime("Posts without titles");
    }

    private void HandleDuplicates(IList<MarkdownModel> models)
    {
        _stopwatch.Restart();
        var uniquePosts = new Dictionary<string, MarkdownModel>();
        var duplicates = new List<MarkdownModel>();

        foreach (var model in models.ToList())
        {
            var key = $"{model.Title}-{model.Date?.Year}";
            if (!uniquePosts.TryAdd(key, model))
            {
                duplicates.Add(model);
                duplicates.Add(uniquePosts[key]);
                models.Remove(uniquePosts[key]);
                models.Remove(model);
            }
        }

        if (duplicates.Any())
        {
            _logger.LogInformation("Found {Count} duplicates", duplicates.Count / 2);
            foreach (var dup in duplicates)
            {
                _logger.LogInformation("Duplicate: {Title} - {Path}", dup.Title, dup.Path);
            }
        }

        LogProcessingTime("Duplicates");
    }

    private void ProcessDeletedFiles(IList<MarkdownModel> currentModels, IList<MarkdownModel> cachedModels)
    {
        _stopwatch.Restart();
        var currentPaths = new HashSet<string>(currentModels.Select(m => m.Path));
        var deletedFiles = cachedModels.Where(m => !currentPaths.Contains(m.Path)).ToList();

        if (deletedFiles.Any())
        {
            _logger.LogInformation("Found {Count} deleted files", deletedFiles.Count);
            foreach (var model in deletedFiles)
            {
                _logger.LogInformation("Deleted: {Title} - {Path}", model.Title, model.Path);
            }
        }

        LogProcessingTime("Deleted files");
    }

    private void LogProcessingTime(string operation)
    {
        _stopwatch.Stop();
        _logger.LogInformation("{Operation} processing completed in {ElapsedMilliseconds}ms",
            operation, _stopwatch.ElapsedMilliseconds);
    }
}