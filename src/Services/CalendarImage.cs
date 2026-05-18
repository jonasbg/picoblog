public static class CalendarImage
{
    public static string? ForYear(int? year, IEnumerable<MarkdownModel> posts)
    {
        var post = posts
            .Where(p => p.Date?.Year == year && HasCoverImage(p))
            .OrderByDescending(p => p.Date)
            .FirstOrDefault();

        return UrlFor(post);
    }

    private static bool HasCoverImage(MarkdownModel post)
    {
        return !string.IsNullOrWhiteSpace(post.CoverImage);
    }

    private static string? UrlFor(MarkdownModel? post)
    {
        if (post?.Date == null || string.IsNullOrWhiteSpace(post.Title) || string.IsNullOrWhiteSpace(post.CoverImage))
            return null;

        return $"/post/{post.Date.Value.Year}/{ContentSecurity.UrlEncodePathSegment(post.Title)}/{ContentSecurity.UrlEncodePathSegment(post.CoverImage)}";
    }
}
