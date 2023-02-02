namespace picoblog.Models;

public static class Config{
  public static bool Synology => Environment.GetEnvironmentVariable("SYNOLOGY_SUPPORT") == "true";
  public static string? Password => Environment.GetEnvironmentVariable("PASSWORD");
  public static string DataDir => Environment.GetEnvironmentVariable("DATA_DIR");
  public static string Domain => $"https://{Environment.GetEnvironmentVariable("DOMAIN")}";
  public static string SynologySize() {
    var size = Environment.GetEnvironmentVariable("SYNOLOGY_SIZE");
    var defaultSize = "XL";
    var allowedSizes = new string[] {"SM", "M", "XL"};
    if(!allowedSizes.Any(p => p == size.ToUpper()))
    {
      Console.WriteLine($"WRONG SYNOLOGY_SIZE SELECTED {size}. DEFAULTING TO {defaultSize}");
      return $"SYNOPHOTO_THUMB_{size.ToUpper()}.jpg";
    }
    return $"SYNOPHOTO_THUMB_{defaultSize}.jpg";
  }
}