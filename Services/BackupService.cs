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
        string sourceDirectory = Config.DataDir;
        
        // Create the backup directory if it doesn't exist
        Directory.CreateDirectory(backupDirectory);
        
        // Use the find and tar commands to create the tar.bz2 archive, including only Markdown files
        Process process = new Process();
        process.StartInfo.FileName = "bash";
        process.StartInfo.Arguments = $"-c \"find {sourceDirectory} -name '*.md' | tar -cvjf {backupFile} -T -\"";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        
        process.Start();
        process.WaitForExit();
        
        _logger.LogInformation($"Backup created: {backupFile}");
    }
}
