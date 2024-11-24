public class MonitorLoop
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;
    private DateTime _lastSync = DateTime.MinValue;

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
        // Enqueue a background work item
        await _taskQueue.QueueBackgroundWorkItemAsync(BuildWorkItem);
    }

    private async ValueTask BuildWorkItem(CancellationToken token)
    {
        var guid = Guid.NewGuid().ToString();

        try
        {
            if (_lastSync == DateTime.MinValue || _lastSync <= DateTime.Now.AddMinutes(-5))
            {
                _logger.LogInformation("Queued Background Task {Guid} is starting.", guid);
                FindFiles();
            }
            _lastSync = DateTime.Now;
        }
        catch (OperationCanceledException)
        {
            // Prevent throwing if the Delay is cancelled
        }
    }

    private void FindFiles()
    {
        _logger.LogInformation("Starting searching for markdown files (*.md)");
        var files = Directory.EnumerateFiles(Config.DataDir, "*.md", SearchOption.AllDirectories);
        var concurrentModels = new ConcurrentBag<MarkdownModel>();

        Parallel.ForEach(
            files,
            file =>
            {
                string content = File.ReadAllText(file);
                Match match = Regex.Match(content, @"^---\n(.*?)\n---", RegexOptions.Singleline);

                if (match.Success)
                    ProcessFrontMatter(match.Groups[1].Value, file, concurrentModels);
            }
        );

        var models = concurrentModels.ToList();
        ProcessResults(models);
        Cache.Models = models;
    }

    private void ProcessFrontMatter(
        string frontmatter,
        string file,
        ConcurrentBag<MarkdownModel> models
    )
    {
        var model = new MarkdownModel();
        foreach (var line in frontmatter.Split('\n'))
        {
            string[] parts = line.Split(':', 2);
            if (parts.Length < 2)
                continue;

            string key = parts[0].Trim();
            string value = parts[1].Trim();

            model.Path = file;
            if (key.Equals(MetadataHeader.Public, StringComparison.InvariantCultureIgnoreCase))
                model.Public = value.Equals("true", StringComparison.InvariantCultureIgnoreCase);
            else if (key.Equals(MetadataHeader.Title, StringComparison.InvariantCultureIgnoreCase))
                model.Title = value;
            else if (key.Equals(MetadataHeader.Date, StringComparison.InvariantCultureIgnoreCase))
                model.Date = DateTime.Parse(value);
            else if (key.Equals(MetadataHeader.Draft, StringComparison.InvariantCultureIgnoreCase))
                model.Visible = value.ToLower() != "true";
            else if (
                key.Equals(MetadataHeader.CoverImage, StringComparison.InvariantCultureIgnoreCase)
            )
                model.CoverImage = value;
            else if (
                key.Equals(MetadataHeader.Description, StringComparison.InvariantCultureIgnoreCase)
            )
                model.Description = value;
        }
        _logger.LogInformation(
            "FOUND: {Title} - Path: {Path} - URL: {Url}",
            model.Title,
            model.Path,
            $"{Config.Domain}/post/{model.Title}"
        );
        models.Add(model);
    }

private void ProcessResults(IList<MarkdownModel> models)
{
    // Process hidden posts
    var hiddenPosts = models.Where(p => p.Visible == false).ToList();
    if (hiddenPosts.Any())
    {
        _logger.LogInformation($"FOUND {hiddenPosts.Count} HIDDEN POSTS");
        foreach (var model in hiddenPosts)
        {
            _logger.LogInformation(
                $"HIDDEN POST: Title: {model.Title} - {Config.Domain}/post/{model.Date?.Year}/{model.Title}"
            );
        }
    }

    // Remove posts without titles in a single pass
    var postsWithoutTitles = new List<MarkdownModel>();
    models = models.Where(p =>
    {
        if (string.IsNullOrEmpty(p.Title))
        {
            postsWithoutTitles.Add(p);
            return false;
        }
        return true;
    }).ToList();

    if (postsWithoutTitles.Any())
    {
        _logger.LogInformation($"FOUND {postsWithoutTitles.Count} POSTS WITHOUT TITLES");
        foreach (var model in postsWithoutTitles)
        {
            _logger.LogInformation($"POST WITHOUT TITLE: {model.Path}");
        }
    }

    // Handle duplicates more efficiently using Dictionary
    var uniquePosts = new Dictionary<string, MarkdownModel>();
    var duplicates = new List<MarkdownModel>();

    foreach (var model in models)
    {
        if (!uniquePosts.TryAdd(model.Title, model))
        {
            // This is a duplicate
            duplicates.Add(model);
            duplicates.Add(uniquePosts[model.Title]); // Add the original one too for logging
            uniquePosts.Remove(model.Title); // Remove the duplicated entry altogether
        }
    }

    if (duplicates.Any())
    {
        _logger.LogInformation("FOUND DUPLICATES, REMOVED FROM SET");
        foreach (var dup in duplicates)
        {
            _logger.LogInformation(
                "Duplicate found: Title: {Title}, Path: {Path}",
                dup.Title,
                dup.Path
            );
        }
    }

    // Set the final models list
    models = uniquePosts.Values.ToList();

    // Check for deleted files
    var deletedFiles = Cache.Models.Where(p => !models.Any(n => n.Path == p.Path)).ToList();
    if (deletedFiles.Any())
    {
        _logger.LogInformation("FOUND DELETED FILES");
        foreach (var del in deletedFiles)
        {
            _logger.LogInformation(
                "DELETED FILE: Title: {Title}, Path: {Path}",
                del.Title,
                del.Path
            );
        }
    }
}
}
