using System;
using System.Diagnostics;
using System.IO;
using picoblog.Models;

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
        string sourceDirectory = Config.DataDir;
        string backupFile = $"{Config.ConfigDir}/{DateTime.Now:yyyy-MM-dd}.tar.bz2";

        // Use the tar command to create the tar.gz archive
        Process process = new Process();
        process.StartInfo.FileName = "tar";
        process.StartInfo.Arguments = $"-cvjf {backupFile} -C {sourceDirectory} ."; // Include subdirectories
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        process.Start();
        process.WaitForExit();

        _logger.LogInformation($"Backup created: {backupFile}");
    }
}