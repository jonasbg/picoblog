using System;
using System.Diagnostics;
using System.IO;

public class BackupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRunTime = now.Date.AddDays(1); // Next midnight
            var delay = nextRunTime - now;

            await Task.Delay(delay, stoppingToken); // Wait until midnight

            PerformBackup(); // Your backup logic here
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

        Console.WriteLine($"Backup created: {backupFile}");
    }
}
