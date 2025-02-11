namespace picoblog.Models;

public static class Cache
{
    public static IList<MarkdownModel> Models = new List<MarkdownModel>();
    public static IList<MarkdownModel> PrivatePosts = new List<MarkdownModel>();
}
