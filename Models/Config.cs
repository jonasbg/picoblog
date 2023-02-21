namespace picoblog.Models;

public static class Config{
  public static string CustomHeader => Environment.GetEnvironmentVariable("CUSTOM_HEADER");
  public static string ConfigDir => Environment.GetEnvironmentVariable("CONFIG_DIR");
  public static bool Synology => Environment.GetEnvironmentVariable("SYNOLOGY_SUPPORT") == "true";
  public static string? Password => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PASSWORD")) ? null : Environment.GetEnvironmentVariable("PASSWORD");
  public static string DataDir => Environment.GetEnvironmentVariable("DATA_DIR");
  public static string Domain => $"https://{Environment.GetEnvironmentVariable("DOMAIN")}";
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