using picoblog.Models;

public class MonitorLoop
{
  private readonly IBackgroundTaskQueue _taskQueue;
  private readonly ILogger _logger;
  private readonly CancellationToken _cancellationToken;
  private DateTime _lastSync;

  public MonitorLoop(IBackgroundTaskQueue taskQueue,
      ILogger<MonitorLoop> logger,
      IHostApplicationLifetime applicationLifetime)
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
      if (_lastSync == null || _lastSync <= DateTime.Now.AddMinutes(-5))
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
    var files = Directory.GetFiles(Config.DataDir, "*.md", SearchOption.AllDirectories);
    var models = new List<MarkdownModel>();
    foreach (var file in files)
    {
      Console.Write($"Found file: {file} ");
      int counter = 0;
      var model = new MarkdownModel();

      foreach (string line in System.IO.File.ReadLines(file))
      {
        if (counter == 0 && !line.Trim().Equals("---"))
        {
          break;
        }
        if (counter > 0 && counter < 10)
        {
          if (line.StartsWith("public", StringComparison.InvariantCultureIgnoreCase))
          {
            if (line.Trim().EndsWith("true", StringComparison.InvariantCultureIgnoreCase))
            {
              model.Public = true;
              model.Path = file;
              if (!models.Any(p => p.Path == model.Path))
              {
                System.Console.WriteLine($"{model.Path} exists in Cache IGNORING");
                models.Add(model);
              }
            }
          }
          if (line.Trim().StartsWith(MetadataHeader.Title, StringComparison.InvariantCultureIgnoreCase))
          {
            var title = line.Split(':')[1].Trim();
            model.Title = title;
          }
          if (line.Trim().StartsWith(MetadataHeader.Date, StringComparison.InvariantCultureIgnoreCase))
          {
            var date = line.Replace("date:", "", System.StringComparison.InvariantCultureIgnoreCase).Trim();
            model.Date = DateTime.Parse(date);
          }
          if (line.Trim().StartsWith(MetadataHeader.Draft, StringComparison.InvariantCultureIgnoreCase))
          {
            var draft = line.Split(':')[1].Trim().ToLower();
            model.Visible = draft != "true";
          }
          if (line.Trim().StartsWith(MetadataHeader.CoverImage, StringComparison.InvariantCultureIgnoreCase))
          {
            var cover = line.Split(':')[1].Trim();
            model.CoverImage = $"{cover}";
          }
          if (line.Trim().StartsWith(MetadataHeader.Description, StringComparison.InvariantCultureIgnoreCase))
          {
            var description = line.Split(':')[1].Trim();
            model.Description = description;
          }

          if (line.Trim().Equals("---"))
            break;
        }
        if (counter >= 10)
          break;
        counter++;
      }
      if (models.LastOrDefault() == model)
      {
        Console.Write("ADDED");
        if (model.Visible)
          Console.WriteLine($" ->  {Config.Domain}/post/{model.Title}");
        else
          Console.WriteLine();
      }

      if (models.LastOrDefault() != model)
        Console.WriteLine("IGNORED");
    }

    if (models.Any(p => p.Visible == false))
    {
      Console.WriteLine($"FOUND {models.Where(p => p.Visible == false).Count()} HIDDEN POSTS");
      foreach (var model in models.Where(p => p.Visible == false))
        Console.WriteLine($"{Config.Domain}/post/{model.Title}");
    }

    if (models.Any(p => string.IsNullOrEmpty(p.Title)))
    {
      Console.WriteLine($"FOUND {models.Where(p => string.IsNullOrEmpty(p.Title)).Count()} POSTS WITHOUT TITLES");
      foreach (var model in models.Where(p => string.IsNullOrEmpty(p.Title)))
      { Console.WriteLine($"{model.Path}"); }
      models = models.Where(p => !string.IsNullOrEmpty(p.Title)).ToList();
    }

    var duplicates = models.GroupBy(p => p.Title).Where(g => g.Count() >= 2).Select(p => p.Key);
    if (duplicates.Any()){
      System.Console.WriteLine($"FOUND DUPLICATES, REMOVED FROM SET");
      foreach (var title in duplicates)
      {
        var dups = models.Where(p => p.Title == title);
        foreach(var dup in dups)
          System.Console.WriteLine($"[{dup.Title}] {dup.Path} ");
      }
      models = models.Where(p => !duplicates.Contains(p.Title)).ToList();
    }
    var deleted = Cache.Models.Where(p => !models.Any(n => n.Path == p.Path));
    if(deleted.Any()){
      System.Console.WriteLine("FOUND DELETED FILES");
      foreach (var del in deleted)
        System.Console.WriteLine($"{del}");
    }

    Cache.Models = models;
  }
}