public class MarkdownModel
{
    private string? _markdown;

    public string? Markdown
    {
        get => _markdown;
        set
        {
            _markdown = value;
            if (string.IsNullOrEmpty(_markdown))
                return;

            var match = Regex.Match(_markdown, @"^---\r?\n(.*?)\r?\n---", RegexOptions.Singleline);

            if (match.Success && match.Groups.Count > 1)
            {
                var frontmatterText = match.Groups[1].Value;
                var frontmatter = Regex.Split(frontmatterText, "\r?\n");
                Title = frontmatter
                    .SingleOrDefault(p => p.StartsWith("title:"))
                    ?.Split(':', 2)[1]
                    .Trim() ?? "";
                Description = frontmatter
                    .SingleOrDefault(p => p.StartsWith("description:"))
                    ?.Split(':', 2)[1]
                    .Trim();
                CoverImage = frontmatter
                    .SingleOrDefault(p => p.StartsWith(MetadataHeader.CoverImage))
                    ?.Split(':', 2)[1]
                    .Trim();
                Public = bool.Parse(
                    frontmatter
                        .SingleOrDefault(p => p.StartsWith("public:"))
                        ?.Split(':', 2)[1]
                        .Trim() ?? "false"
                );
                Draft = bool.Parse(
                    frontmatter
                        .SingleOrDefault(p => p.StartsWith("draft:"))
                        ?.Split(':', 2)[1]
                        .Trim() ?? "false"
                );

                var dateString = frontmatter
                    .SingleOrDefault(p => p.StartsWith("date:"))
                    ?.Split(':', 2)[1]
                    .Trim();
                if (DateTime.TryParse(dateString, out DateTime date))
                {
                    Date = date;
                }

                Location = LocationParser.ParseLocation(frontmatterText);
            }
        }
    }
    public string Title { get; internal set; } = "";
    public bool Public { get; internal set; } = false;
    public string Path { get; internal set; } = "";
    public DateTime? Date { get; internal set; }
    public string? CoverImage { get; internal set; }
    public string? Description { get; internal set; }
    public bool Draft { get; internal set; } = false;
    public LocationMetadata? Location { get; internal set; }
}
