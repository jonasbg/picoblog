namespace picoblog.Models;

public class MarkdownModel
{
  private string? _markdown;

  public string? Markdown { get => _markdown; set {
      _markdown = value;
      var match = Regex.Match(_markdown, @"^---\r?\n(.*?)\r?\n---", RegexOptions.Singleline);

      if (match.Success && match.Groups.Count > 1)
      {
          var frontmatter = match.Groups[1].Value.Split(System.Environment.NewLine);
          CoverImage = frontmatter.SingleOrDefault(p => p.StartsWith(MetadataHeader.CoverImage))?.Split(':')[1].Trim();
      }
    }
  }
  public string Title { get; internal set; }
  public bool Public { get; internal set; } = false;
  public string Path { get; internal set; }
  public DateTime? Date { get; internal set; }
  public string? CoverImage { get; internal set; }
  public bool Visible { get; internal set; } = true;
  public string? Description { get; internal set; }
}
