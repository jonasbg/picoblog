using System.Runtime.InteropServices;

public class MonitorLoop : IDisposable
{
    private readonly ILogger _logger;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, MarkdownModel> _modelCache;
    private readonly ConcurrentDictionary<string, MarkdownModel> _privatePostsCache;
    private readonly object _updateLock = new object();
    private bool _disposed;

    private static readonly string[] ExcludedDirectories = new[]
    {
        "@eaDir", // Synology thumbnail directory
        "#recycle", // Synology recycle bin
        ".DS_Store", // Mac metadata
        "@Recycle", // Another variant of recycle
        "$RECYCLE.BIN", // Windows recycle bin
        "System Volume Information", // Windows system folder
    };

    public MonitorLoop(ILogger<MonitorLoop> logger, IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _modelCache = new ConcurrentDictionary<string, MarkdownModel>();
        _privatePostsCache = new ConcurrentDictionary<string, MarkdownModel>();

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
        {
            _logger.LogInformation("Production environment detected - using polling mode");
            _watcher = null;
        }
        else
        {
            try
            {
                EnsureInotifyLimits();

                _watcher = new FileSystemWatcher(Config.DataDir)
                {
                    Filter = "*.md",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = false,
                    NotifyFilter =
                        NotifyFilters.FileName
                        | NotifyFilters.DirectoryName
                        | NotifyFilters.LastWrite,
                };

                _watcher.Created += FilterExcludedDirectories;
                _watcher.Changed += FilterExcludedDirectories;
                _watcher.Deleted += FilterExcludedDirectories;
                _watcher.Renamed += FilterExcludedDirectoriesRenamed;

                // Register for cancellation
                applicationLifetime.ApplicationStopping.Register(() =>
                {
                    Dispose();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to initialize FileSystemWatcher. Falling back to polling."
                );
                _watcher = null;
            }
        }
    }

    private void FilterExcludedDirectoriesRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsExcludedPath(e.FullPath) && !IsExcludedPath(e.OldFullPath))
        {
            OnFileRenamed(sender, e);
        }
    }

    private void FilterExcludedDirectories(object sender, FileSystemEventArgs e)
    {
        if (!IsExcludedPath(e.FullPath))
        {
            OnFileChanged(sender, e);
        }
    }

    private bool IsExcludedPath(string path)
    {
        return ExcludedDirectories.Any(dir =>
            path.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar)
            || path.EndsWith(Path.DirectorySeparatorChar + dir)
        );
    }

    private void EnsureInotifyLimits()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                // Check current limits
                var maxWatches = File.ReadAllText("/proc/sys/fs/inotify/max_user_watches");
                var maxInstances = File.ReadAllText("/proc/sys/fs/inotify/max_user_instances");

                _logger.LogInformation(
                    "Current inotify limits - max_user_watches: {Watches}, max_user_instances: {Instances}",
                    maxWatches.Trim(),
                    maxInstances.Trim()
                );

                // Log instructions if limits are too low
                if (int.TryParse(maxWatches.Trim(), out int watches) && watches < 524288)
                {
                    _logger.LogWarning(
                        @"Low inotify watch limit detected. To increase, run:
                        echo fs.inotify.max_user_watches=524288 | sudo tee -a /etc/sysctl.conf
                        sudo sysctl -p"
                    );
                }

                if (int.TryParse(maxInstances.Trim(), out int instances) && instances < 256)
                {
                    _logger.LogWarning(
                        @"Low inotify instances limit detected. To increase, run:
                        echo fs.inotify.max_user_instances=256 | sudo tee -a /etc/sysctl.conf
                        sudo sysctl -p"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check inotify limits");
            }
        }
    }

    public void StartMonitorLoop()
    {
        // Initial load of all files
        LoadAllFiles();

        if (_watcher != null)
        {
            try
            {
                // Disable watching subdirectories to avoid hitting inotify limits
                _watcher.IncludeSubdirectories = false;

                // Setup watchers but filter out excluded directories
                _watcher.Created += FilterExcludedDirectories;
                _watcher.Changed += FilterExcludedDirectories;
                _watcher.Deleted += FilterExcludedDirectories;
                _watcher.Renamed += FilterExcludedDirectoriesRenamed;
                _watcher.Error += OnWatcherError;

                // Start watching
                _watcher.EnableRaisingEvents = true;
                _logger.LogInformation(
                    "Started watching (top-level only) for markdown files in {Directory}",
                    Config.DataDir
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start FileSystemWatcher. Falling back to polling.");
                StartPolling();
            }
        }
        else
        {
            StartPolling();
        }
    }

    private void StartPolling()
    {
        _logger.LogInformation("Starting polling-based file monitoring");
        var timer = new Timer(PollForChanges, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    private void PollForChanges(object state)
    {
        try
        {
            LoadAllFiles();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file polling");
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher error occurred");

        try
        {
            // Try to restart the watcher
            if (_watcher != null && !_disposed)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.EnableRaisingEvents = true;
                _logger.LogInformation("FileSystemWatcher restarted after error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart FileSystemWatcher. Falling back to polling.");
            StartPolling();
        }
    }

    private void LoadAllFiles()
    {
        _logger.LogInformation("Loading all existing markdown files");
        var files = Directory.EnumerateFiles(Config.DataDir, "*.md", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            ProcessFile(file);
        }

        UpdateCaches();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Add small delay to ensure file is completely written
            Thread.Sleep(100);
            ProcessFile(e.FullPath);
            UpdateCaches();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file change for {Path}", e.FullPath);
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            _modelCache.TryRemove(e.FullPath, out _);
            _privatePostsCache.TryRemove(e.FullPath, out _);
            _logger.LogInformation("DELETED FILE: {Path}", e.FullPath);
            UpdateCaches();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file deletion for {Path}", e.FullPath);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            // Remove old path
            _modelCache.TryRemove(e.OldFullPath, out _);
            _privatePostsCache.TryRemove(e.OldFullPath, out _);

            // Process with new path
            ProcessFile(e.FullPath);
            UpdateCaches();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing file rename from {OldPath} to {NewPath}",
                e.OldFullPath,
                e.FullPath
            );
        }
    }

    private void ProcessFile(string filePath)
    {
        string content = File.ReadAllText(filePath);
        Match match = Regex.Match(content, @"^---\n(.*?)\n---", RegexOptions.Singleline);

        if (match.Success)
        {
            var model = ProcessFrontMatter(match.Groups[1].Value, filePath);
            if (model != null)
            {
                if (model.Public && !model.Draft)
                {
                    _modelCache[filePath] = model;
                    _logger.LogInformation(
                        "UPDATED: {Title} - Path: {Path} - URL: {Url}",
                        model.Title,
                        model.Path,
                        $"{Config.Domain}/post/{model.Title}"
                    );
                }
                else if (!model.Public && !model.Draft)
                {
                    _privatePostsCache[filePath] = model;
                }
                else if (model.Draft)
                {
                    _logger.LogInformation("FOUND DRAFT (IGNORING): {Path}", model.Path);
                }
            }
        }
    }

    private MarkdownModel ProcessFrontMatter(string frontmatter, string file)
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
                    model.Date = DateTime.Parse(value);
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

    private void UpdateCaches()
    {
        lock (_updateLock)
        {
            var models = _modelCache.Values.ToList();
            var privatePosts = _privatePostsCache.Values.ToList();

            // Check for posts without titles
            var postsWithoutTitles = models.Where(p => string.IsNullOrEmpty(p.Title)).ToList();
            if (postsWithoutTitles.Any())
            {
                _logger.LogInformation(
                    "FOUND {Count} POSTS WITHOUT TITLES",
                    postsWithoutTitles.Count
                );
                foreach (var model in postsWithoutTitles)
                {
                    _logger.LogInformation("POST WITHOUT TITLE: {Path}", model.Path);
                    _modelCache.TryRemove(model.Path, out _);
                }
                models = models.Where(p => !string.IsNullOrEmpty(p.Title)).ToList();
            }

            // Check for duplicates
            var duplicates = models
                .GroupBy(p => p.Title)
                .Where(g => g.Count() >= 2)
                .SelectMany(g => g.Skip(1))
                .ToList();

            if (duplicates.Any())
            {
                _logger.LogInformation("FOUND DUPLICATES, REMOVING EXTRAS");
                foreach (var dup in duplicates)
                {
                    _logger.LogInformation(
                        "REMOVING DUPLICATE: Title: {Title}, Path: {Path}",
                        dup.Title,
                        dup.Path
                    );
                    _modelCache.TryRemove(dup.Path, out _);
                }
                models = models.Except(duplicates).ToList();
            }

            // Update the global cache
            Cache.Models = models;
            Cache.PrivatePosts = privatePosts;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileChanged;
            _watcher.Changed -= OnFileChanged;
            _watcher.Deleted -= OnFileDeleted;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();
        }
    }
}
