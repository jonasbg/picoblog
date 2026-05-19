namespace picoblog.Models;

public sealed record SearchResult(
    string Title,
    string Url,
    string? CoverImageUrl,
    string Excerpt,
    string? DateText,
    double Score
);
