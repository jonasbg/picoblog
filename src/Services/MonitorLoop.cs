public class MonitorLoop
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<MonitorLoop> _logger;
    private readonly CancellationToken _cancellationToken;
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);
    private DateTime _lastSync = DateTime.MinValue;

    public MonitorLoop(
        IBackgroundTaskQueue taskQueue,
        ILogger<MonitorLoop> logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationToken = applicationLifetime?.ApplicationStopping
            ?? throw new ArgumentNullException(nameof(applicationLifetime));
    }

    public void StartMonitorLoop()
    {
        Task.Run(async () => await _taskQueue.QueueBackgroundWorkItemAsync(ProcessFilesAsync));
    }

    private async ValueTask ProcessFilesAsync(CancellationToken token)
    {
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            if (ShouldSync())
            {
                _logger.LogInformation("Starting background task {CorrelationId}", correlationId);
                await FindFilesAsync(token);
                _lastSync = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _logger.LogInformation("Task {CorrelationId} was cancelled", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing files in task {CorrelationId}", correlationId);
        }
    }

    private bool ShouldSync() =>
        _lastSync == DateTime.MinValue || DateTime.UtcNow - _lastSync >= _syncInterval;

    private async Task FindFilesAsync(CancellationToken token)
    {
        _logger.LogInformation("Searching for markdown files (*.md)");

        var files = await Task.Run(() =>
            Directory.EnumerateFiles(Config.DataDir, "*.md", SearchOption.AllDirectories), token);

        var models = new ConcurrentBag<MarkdownModel>();

        await Parallel.ForEachAsync(files, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = token
        }, async (file, token) =>
        {
            var content = await File.ReadAllTextAsync(file, token);
            var model = await ParseMarkdownFileAsync(file, content, token);
            if (model != null)
            {
                models.Add(model);
                _logger.LogInformation("Processed: {Title} - Path: {Path} - URL: {Url}",
                    model.Title, model.Path, $"{Config.Domain}/post/{model.Title}");
            }
        });

        var processedModels = await ProcessResultsAsync(models.ToList(), token);
        Cache.Models = processedModels;
    }

    private async Task<MarkdownModel> ParseMarkdownFileAsync(string filePath, string content, CancellationToken token)
    {
        var match = Regex.Match(content, @"^---\n(.*?)\n---", RegexOptions.Singleline);
        if (!match.Success) return null;

        var model = new MarkdownModel { Path = filePath };
        var frontMatter = match.Groups[1].Value;

        await Task.Run(() =>
        {
            var frontMatterLines = frontMatter.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in frontMatterLines)
            {
                if (!line.Contains(':')) continue;

                var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2) continue;

                ProcessMetadataField(model, parts[0], parts[1]);
            }
        }, token);

        return model;
    }

    private void ProcessMetadataField(MarkdownModel model, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case var k when k.Equals(MetadataHeader.Public, StringComparison.OrdinalIgnoreCase):
                model.Public = bool.Parse(value);
                break;
            case var k when k.Equals(MetadataHeader.Title, StringComparison.OrdinalIgnoreCase):
                model.Title = value;
                break;
            case var k when k.Equals(MetadataHeader.Date, StringComparison.OrdinalIgnoreCase):
                model.Date = DateTime.Parse(value);
                break;
            case var k when k.Equals(MetadataHeader.Draft, StringComparison.OrdinalIgnoreCase):
                model.Visible = !value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
            case var k when k.Equals(MetadataHeader.CoverImage, StringComparison.OrdinalIgnoreCase):
                model.CoverImage = value;
                break;
            case var k when k.Equals(MetadataHeader.Description, StringComparison.OrdinalIgnoreCase):
                model.Description = value;
                break;
        }
    }

    private async Task<List<MarkdownModel>> ProcessResultsAsync(List<MarkdownModel> models, CancellationToken token)
    {
        await Task.Run(() =>
        {
            LogHiddenPosts(models);
            LogPostsWithoutTitles(models);
            LogDuplicates(models);
            LogDeletedFiles(models);
        }, token);

        return models
            .Where(p => !string.IsNullOrEmpty(p.Title))
            .GroupBy(p => p.Title)
            .Where(g => g.Count() == 1)
            .Select(g => g.First())
            .ToList();
    }

    private void LogHiddenPosts(IEnumerable<MarkdownModel> models)
    {
        var hiddenPosts = models.Where(p => !p.Visible).ToList();
        if (!hiddenPosts.Any()) return;

        _logger.LogInformation("Found {Count} hidden posts", hiddenPosts.Count);
        foreach (var post in hiddenPosts)
        {
            _logger.LogInformation("Hidden post: {Title} - {Url}",
                post.Title, $"{Config.Domain}/post/{post.Date?.Year}/{post.Title}");
        }
    }

    private void LogPostsWithoutTitles(IEnumerable<MarkdownModel> models)
    {
        var postsWithoutTitles = models.Where(p => string.IsNullOrEmpty(p.Title)).ToList();
        if (!postsWithoutTitles.Any()) return;

        _logger.LogInformation("Found {Count} posts without titles", postsWithoutTitles.Count);
        foreach (var post in postsWithoutTitles)
        {
            _logger.LogInformation("Post without title: {Path}", post.Path);
        }
    }

    private void LogDuplicates(IEnumerable<MarkdownModel> models)
    {
        var duplicates = models
            .GroupBy(p => p.Title)
            .Where(g => g.Count() > 1)
            .ToList();

        if (!duplicates.Any()) return;

        _logger.LogInformation("Found duplicates, removing from set");
        foreach (var group in duplicates)
        {
            foreach (var post in group)
            {
                _logger.LogInformation("Duplicate found: {Title} - {Path}", post.Title, post.Path);
            }
        }
    }

    private void LogDeletedFiles(IEnumerable<MarkdownModel> models)
    {
        var deleted = Cache.Models.Where(p => !models.Any(n => n.Path == p.Path)).ToList();
        if (!deleted.Any()) return;

        _logger.LogInformation("Found {Count} deleted files", deleted.Count);
        foreach (var post in deleted)
        {
            _logger.LogInformation("Deleted file: {Title} - {Path}", post.Title, post.Path);
        }
    }
}