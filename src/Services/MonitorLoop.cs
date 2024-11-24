public class MonitorLoop
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;
    private DateTime _lastSync = DateTime.MinValue;
    private readonly ParallelOptions _parallelOptions;
    private readonly Channel<MarkdownModel> _channel;
    private readonly ChannelWriter<MarkdownModel> _channelWriter;

    public MonitorLoop(
        IBackgroundTaskQueue taskQueue,
        ILogger<MonitorLoop> logger,
        IHostApplicationLifetime applicationLifetime
    )
    {
        _taskQueue = taskQueue;
        _logger = logger;
        _cancellationToken = applicationLifetime.ApplicationStopping;

        _parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
            CancellationToken = _cancellationToken,
        };

        _channel = Channel.CreateUnbounded<MarkdownModel>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );
        _channelWriter = _channel.Writer;
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

    private async Task FindFilesAsync()
    {
        _logger.LogInformation("Starting searching for markdown files (*.md)");

        // Get all files first
        var files = Directory
            .EnumerateFiles(Config.DataDir, "*.md", SearchOption.AllDirectories)
            .ToList();

        // Process files in batches
        const int batchSize = 100;
        for (int i = 0; i < files.Count; i += batchSize)
        {
            var batch = files.Skip(i).Take(batchSize).ToList();
            await ProcessFileBatchAsync(batch);
        }

        // Complete the channel
        _channelWriter.Complete();

        // Read all models from the channel
        var models = await ReadModelsFromChannelAsync();
        ProcessResults(models);
        Cache.Models = models;
    }

    private async Task ProcessFileBatchAsync(List<string> files)
    {
        await Parallel.ForEachAsync(
            files,
            _parallelOptions,
            async (file, token) =>
            {
                try
                {
                    string content = await File.ReadAllTextAsync(file, token);
                    var match = Regex.Match(
                        content,
                        @"^---\n(.*?)\n---",
                        RegexOptions.Singleline | RegexOptions.Compiled
                    );

                    if (match.Success)
                    {
                        await ProcessFrontMatterAsync(match.Groups[1].Value, file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file: {File}", file);
                }
            }
        );
    }

    private async Task ProcessFrontMatterAsync(string frontmatter, string file)
    {
        var model = new MarkdownModel { Path = file };

        foreach (var line in frontmatter.Split('\n'))
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex == -1)
                continue;

            string key = line[..colonIndex].Trim();
            string value = line[(colonIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                continue;

            switch (key.ToLowerInvariant())
            {
                case var k when k.Equals(MetadataHeader.Public, StringComparison.OrdinalIgnoreCase):
                    model.Public = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case var k when k.Equals(MetadataHeader.Title, StringComparison.OrdinalIgnoreCase):
                    model.Title = value;
                    break;
                case var k when k.Equals(MetadataHeader.Date, StringComparison.OrdinalIgnoreCase):
                    model.Date = DateTime.Parse(value);
                    break;
                case var k when k.Equals(MetadataHeader.Draft, StringComparison.OrdinalIgnoreCase):
                    model.Visible = value.ToLower() != "true";
                    break;
                case var k
                    when k.Equals(MetadataHeader.CoverImage, StringComparison.OrdinalIgnoreCase):
                    model.CoverImage = value;
                    break;
                case var k
                    when k.Equals(MetadataHeader.Description, StringComparison.OrdinalIgnoreCase):
                    model.Description = value;
                    break;
            }
        }

        await _channelWriter.WriteAsync(model, _cancellationToken);
        _logger.LogInformation(
            "FOUND: {Title} - Path: {Path} - URL: {Url}",
            model.Title,
            model.Path,
            $"{Config.Domain}/post/{model.Title}"
        );
    }

    private async Task<List<MarkdownModel>> ReadModelsFromChannelAsync()
    {
        var models = new List<MarkdownModel>();
        await foreach (var model in _channel.Reader.ReadAllAsync(_cancellationToken))
        {
            models.Add(model);
        }
        return models;
    }

    // Update the BuildWorkItem method to use the async version
    private async ValueTask BuildWorkItem(CancellationToken token)
    {
        var guid = Guid.NewGuid().ToString();

        try
        {
            if (_lastSync == DateTime.MinValue || _lastSync <= DateTime.Now.AddMinutes(-5))
            {
                _logger.LogInformation("Queued Background Task {Guid} is starting.", guid);
                await FindFilesAsync();
            }
            _lastSync = DateTime.Now;
        }
        catch (OperationCanceledException)
        {
            // Prevent throwing if the Delay is cancelled
        }
    }

    private void ProcessResults(IList<MarkdownModel> models)
    {
        if (models.Any(p => p.Visible == false))
        {
            var hiddenPosts = models.Where(p => p.Visible == false);
            _logger.LogInformation($"FOUND {hiddenPosts.Count()} HIDDEN POSTS");
            foreach (var model in hiddenPosts)
                _logger.LogInformation(
                    $"HIDDEN POST: Title: {model.Title} - {Config.Domain}/post/{model.Date?.Year}/{model.Title}"
                );
        }

        if (models.Any(p => string.IsNullOrEmpty(p.Title)))
        {
            var postsWithoutTitles = models.Where(p => string.IsNullOrEmpty(p.Title));
            _logger.LogInformation($"FOUND {postsWithoutTitles.Count()} POSTS WITHOUT TITLES");

            foreach (var model in postsWithoutTitles)
                _logger.LogInformation($"POST WITHOUT TITLE: {model.Path}");

            models = models.Where(p => !string.IsNullOrEmpty(p.Title)).ToList();
        }

        var duplicates = models.GroupBy(p => p.Title).Where(g => g.Count() >= 2).Select(p => p.Key);
        if (duplicates.Any())
        {
            _logger.LogInformation("FOUND DUPLICATES, REMOVED FROM SET");
            foreach (var title in duplicates)
            {
                var dups = models.Where(p => p.Title == title);
                foreach (var dup in dups)
                    _logger.LogInformation(
                        "Duplicate found: Title: {Title}, Path: {Path}",
                        dup.Title,
                        dup.Path
                    );
            }
            models = models.Where(p => !duplicates.Contains(p.Title)).ToList();
        }
        var deleted = Cache.Models.Where(p => !models.Any(n => n.Path == p.Path));
        if (deleted.Any())
        {
            _logger.LogInformation("FOUND DELETED FILES");
            foreach (var del in deleted)
                _logger.LogInformation(
                    "DELETED FILE: Title: {Title}, Path: {Path}",
                    del.Title,
                    del.Path
                );
        }
    }
}
