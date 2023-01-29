namespace picoblog.Models;

public static class Config{

  public static bool Synology => Environment.GetEnvironmentVariable("SYNOLOGY_SUPPORT") == "true";
  public static string DataDir => Environment.GetEnvironmentVariable("DATA_DIR");
}