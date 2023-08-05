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
        string backupDirectory = Path.Combine(Config.ConfigDir, "backup");
        string backupFile = $"{backupDirectory}/{DateTime.Now:yyyy-MM-dd}.tar.bz2";
        string sourceDirectory = Config.DataDir; // Base directory for relative paths
        
        // Create the backup directory if it doesn't exist
        Directory.CreateDirectory(backupDirectory);
        
        // Create a tar archive
        using (var tarStream = File.OpenWrite(backupFile))
        using (var bz2Stream = new BZip2Stream(tarStream, CompressionMode.Compress, false))
        using (var archive = TarArchive.Create())
        {
            // Add all Markdown files from the Cache.Models to the tar archive
            foreach (var model in Cache.Models)
            {
                var filePath = model.Path;
                var entryName = filePath.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
                archive.AddEntry(entryName, filePath);
            }
        
            // Save the tar archive as a BZip2-compressed file
            archive.SaveTo(bz2Stream, new WriterOptions(CompressionType.BZip2));
        }
        
        _logger.LogInformation($"Backup created: {backupFile}");
    }
}
