namespace picoblog.Models;

public static class Config{
  private static double _cachetime;
  private static int maxheight;
  private static int imagequality;
  public static double CacheTimeInMinutes => double.TryParse(Environment.GetEnvironmentVariable("IMAGE_CACHE_MINUTES"), out _cachetime) ? _cachetime : 4.0;
  public static int ImageMaxSize => int.TryParse(Environment.GetEnvironmentVariable("IMAGE_MAX_SIZE"), out maxheight) ? maxheight : 1280;
  public static int ImageQuality => int.TryParse(Environment.GetEnvironmentVariable("IMAGE_QUALITY"), out imagequality) ? imagequality : 65;
  public static string Title => Environment.GetEnvironmentVariable("TITLE") ?? "Picoblog";
  public static string Description => Environment.GetEnvironmentVariable("DESCRIPTION") ?? "A simple zero fraction blogging platform";
  public static string CustomHeader => Environment.GetEnvironmentVariable("CUSTOM_HEADER");
  public static string ConfigDir => Environment.GetEnvironmentVariable("CONFIG_DIR") ?? "/config";
  public static bool Synology => Environment.GetEnvironmentVariable("SYNOLOGY_SUPPORT") == "true";
  public static string? Password => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PASSWORD")) ? null : Environment.GetEnvironmentVariable("PASSWORD");
  public static string DataDir => Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data";
  public static string Domain => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOMAIN")) ? "http://localhost:8080" : "https://" + Environment.GetEnvironmentVariable("DOMAIN");
  public static bool EnableBackup =>
    Environment.GetEnvironmentVariable("PICOBLOG_ENABLE_BACKUP")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

  public static string SynologySize() {
    var sizeEnv = Environment.GetEnvironmentVariable("SYNOLOGY_SIZE");
    var defaultSize = "XL";
    var allowedSizes = new string[] {"SM", "M", "XL"};
    if(allowedSizes.Any(p => p.Equals(sizeEnv.ToUpper())))
      return $"SYNOPHOTO_THUMB_{sizeEnv.ToUpper()}.jpg";

    Console.WriteLine($"WRONG SYNOLOGY_SIZE SELECTED {sizeEnv}. DEFAULTING TO {defaultSize}");
    return $"SYNOPHOTO_THUMB_{defaultSize}.jpg";
  }
}
