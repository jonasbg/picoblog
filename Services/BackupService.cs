public class BackupService : BackgroundService
{
    private readonly ILogger<BackupService> _logger;

    public BackupService(ILogger<BackupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool enableBackup = Config.EnableBackup;
        if (enableBackup)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRunTime = now.Date.AddDays(1); // Next midnight
                var delay = nextRunTime - now;
                _logger.LogInformation($"Next backup scheduled for: {nextRunTime:yyyy-MM-dd HH:mm}");

                await Task.Delay(delay, stoppingToken); // Wait until midnight

                PerformBackup(); // Your backup logic here
            }
        }
        else
        {
            _logger.LogInformation("Backup is disabled.");
        }
    }

    private void PerformBackup()
    {
        if(!Cache.Models.Any())
            return;

        string backupDirectory = Path.Combine(Config.ConfigDir, "backups");
        string backupFile = $"{backupDirectory}/{DateTime.Now:yyyy-MM-dd}.tar";
        string sourceDirectory = Config.DataDir; // Base directory for relative paths

        _logger.LogDebug($"Backup directory: {backupDirectory}");
        _logger.LogDebug($"Source directory: {sourceDirectory}");

        // Create the backup directory if it doesn't exist
        Directory.CreateDirectory(backupDirectory);
        _logger.LogDebug("Backup directory created or already exists.");

        // Create a tar archive
        using (var archive = TarArchive.Create())
        {
            _logger.LogDebug("Creating tar archive...");
            // Add all Markdown files from the Cache.Models to the tar archive
            foreach (var model in Cache.Models)
            {
                var filePath = model.Path;
                var entryName = filePath.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
                archive.AddEntry(entryName, filePath);
                _logger.LogDebug($"Added file to archive: {filePath}");
            }

            // Save the tar archive as a BZip2-compressed file
            archive.SaveTo(backupFile, new TarWriterOptions(CompressionType.None, true));
            _logger.LogDebug("Tar archive created.");
        }

        _logger.LogInformation($"Backup created: {backupFile}");
    }
}
