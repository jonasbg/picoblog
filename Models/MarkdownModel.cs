namespace picoblog.Models;

public class MarkdownModel
{
  public string? Markdown { get; set; }
  public string? Title { get; internal set; }
  public bool Public { get; internal set; }
  public string Path { get; internal set; }
  public DateTime? Date { get; internal set; }
  public string? PosterPath {get; internal set;}
}