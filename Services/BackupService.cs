using System;
using System.Diagnostics;
using System.IO;

class BackupService
{
    public static void Start(TimeSpan interval)
    {
        System.Threading.Timer _timer = new System.Threading.Timer(BackupTask, null, TimeSpan.Zero, interval);
    }

    private static void BackupTask(object state)
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
