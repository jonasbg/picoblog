namespace picoblog.Models;

public class Payload {
  [Required]
  public string? Title {get;set;}
  public string? Image {get;set;}
}