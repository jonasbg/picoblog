namespace picoblog.Models;

public class MarkdownModel
{
  public string? Markdown { get; set; }
  public string? Title { get; internal set; }
  public bool Public { get; internal set; } = false;
  public string Path { get; internal set; }
  public DateTime? Date { get; internal set; }
  public string? PosterPath {get; internal set;}
  public bool Visible {get; internal set;} = true;
  public string? Description {get; internal set;}
}