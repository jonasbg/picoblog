namespace picoblog.Controllers;

[Route("search")]
[AllowAnonymous]
public class SearchController : Controller
{
    private const int MaxResults = 8;
    private const int MinimumQueryLength = 2;
    private static readonly CultureInfo NorwegianCulture = CultureInfo.GetCultureInfo("nb-NO");

    [HttpGet("suggest")]
    public IActionResult Suggest([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < MinimumQueryLength)
            return Json(Array.Empty<SearchResult>());

        var query = q.Trim();
        var posts = PostsForCurrentUser();
        var results = posts
            .Select(post => ToSearchResult(post, query))
            .Where(result => result is { Score: > 0 })
            .OrderByDescending(result => result!.Score)
            .ThenByDescending(result => result!.DateText)
            .Take(MaxResults)
            .ToArray();

        return Json(results);
    }

    private IEnumerable<MarkdownModel> PostsForCurrentUser()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Cache.Models.Concat(Cache.PrivatePosts);

        return Cache.Models.Where(post => !post.HasPostPassword);
    }

    private static SearchResult? ToSearchResult(MarkdownModel post, string query)
    {
        var score =
            ScoreField(post.Title, query) * 3.0
            + ScoreField(post.Description, query) * 2.0
            + ScoreField(post.SearchText, query);

        if (post.Date is { } date)
        {
            var ageInDays = Math.Max(0, (DateTime.Today - date.Date).TotalDays);
            score += Math.Max(0, 10 - ageInDays / 365);
        }

        if (score < 55)
            return null;

        var title = post.Title;
        var titlePath = ContentSecurity.UrlEncodePathSegment(title);
        var url = $"/post/{post.Date?.Year}/{titlePath}";
        var coverImageUrl = string.IsNullOrWhiteSpace(post.CoverImage)
            ? null
            : $"/post/{post.Date?.Year}/{titlePath}/{ContentSecurity.UrlEncodePathSegment(post.CoverImage)}";

        return new SearchResult(title, url, coverImageUrl, post.SearchExcerpt, FormatDate(post.Date), score);
    }

    private static double ScoreField(string? value, string query)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var normalizedValue = NormalizeForSearch(value);
        var normalizedQuery = NormalizeForSearch(query);
        if (string.IsNullOrWhiteSpace(normalizedValue) || string.IsNullOrWhiteSpace(normalizedQuery))
            return 0;

        if (normalizedValue == normalizedQuery)
            return 120;

        if (normalizedValue.StartsWith(normalizedQuery, StringComparison.Ordinal))
            return 105;

        if (normalizedValue.Contains(normalizedQuery, StringComparison.Ordinal))
            return 90;

        var queryTokens = Tokenize(normalizedQuery).ToArray();
        if (queryTokens.Length == 0)
            return 0;

        var valueTokens = Tokenize(normalizedValue).Take(400).ToArray();
        if (valueTokens.Length == 0)
            return 0;

        var tokenScores = queryTokens.Select(queryToken =>
            valueTokens.Select(valueToken => ScoreToken(valueToken, queryToken)).DefaultIfEmpty(0).Max()
        );

        return tokenScores.Average();
    }

    private static double ScoreToken(string valueToken, string queryToken)
    {
        if (valueToken == queryToken)
            return 100;

        if (valueToken.StartsWith(queryToken, StringComparison.Ordinal))
            return 88;

        if (valueToken.Contains(queryToken, StringComparison.Ordinal))
            return 74;

        var maxLength = Math.Max(valueToken.Length, queryToken.Length);
        if (maxLength == 0)
            return 0;

        var distance = LevenshteinDistance(valueToken, queryToken);
        var similarity = 1 - (double)distance / maxLength;
        return similarity >= 0.68 ? similarity * 70 : 0;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
            previous[column] = column;

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= right.Length; column++)
            {
                var cost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost
                );
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizeForSearch(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static string? FormatDate(DateTime? date)
    {
        if (date == null)
            return null;

        var month = NorwegianCulture.TextInfo.ToTitleCase(date.Value.ToString("MMMM", NorwegianCulture));
        return $"{date.Value.Year}. {month} {date.Value.Day}";
    }
}
