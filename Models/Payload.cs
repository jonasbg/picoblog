using System.ComponentModel.DataAnnotations;
namespace picoblog.Models;

public class Post {
  [Required]
  public string Title {get; set;}
}