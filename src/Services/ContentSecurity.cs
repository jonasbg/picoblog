using AngleSharp.Html.Parser;
using Ganss.Xss;
using Markdig;

public static class ContentSecurity
{
    private static readonly Lazy<HtmlSanitizer> HtmlSanitizer = new(CreateHtmlSanitizer);
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UseAdvancedExtensions()
        .Build();

    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".gif",
            ".heic",
            ".heif",
            ".jpeg",
            ".jpg",
            ".png",
            ".webp",
        };

    public static bool PasswordEquals(string? suppliedPassword, string? configuredPassword)
    {
        if (string.IsNullOrEmpty(suppliedPassword) || string.IsNullOrEmpty(configuredPassword))
            return false;

        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedPassword);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredPassword);
        return CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }

    public static string SanitizeMarkdown(string? markdown, string postTitle)
    {
        var html = Markdown.ToHtml(markdown ?? string.Empty, MarkdownPipeline);
        var titlePath = Uri.EscapeDataString(postTitle);
        html = HtmlSanitizer.Value.Sanitize(html);
        return RewriteMarkdownImageSources(html, titlePath);
    }

    public static string HtmlEncode(string? value)
    {
        return HtmlEncoder.Default.Encode(value ?? string.Empty);
    }

    public static string UrlEncodePathSegment(string? value)
    {
        return Uri.EscapeDataString(value ?? string.Empty);
    }

    public static string CachePathForImage(string imagePath)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imagePath))).ToLowerInvariant();
        return Path.Combine(Config.ConfigDir, "images", $"{hash}.jpg");
    }

    public static bool TryResolvePostImagePath(
        MarkdownModel model,
        string? requestedImage,
        out string resolvedPath,
        out bool isCoverImage
    )
    {
        resolvedPath = string.Empty;
        isCoverImage = false;

        if (!TryNormalizeImageReference(requestedImage, out var imageReference))
            return false;

        isCoverImage = string.Equals(model.CoverImage, imageReference, StringComparison.Ordinal);
        var isMarkdownImage = model.Markdown?.Contains(imageReference, StringComparison.Ordinal) == true;
        if (!isCoverImage && !isMarkdownImage)
            return false;

        var postDirectory = Path.GetDirectoryName(model.Path);
        if (string.IsNullOrEmpty(postDirectory))
            return false;

        var root = Path.GetFullPath(postDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, imageReference));
        if (!IsPathInsideDirectory(candidate, root))
            return false;

        resolvedPath = candidate;
        return true;
    }

    private static bool TryNormalizeImageReference(string? image, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(image))
            return false;

        try
        {
            normalized = Uri.UnescapeDataString(image).Replace('\\', '/').TrimStart('/');
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (
            string.IsNullOrWhiteSpace(normalized)
            || normalized.IndexOf('\0') >= 0
            || normalized.StartsWith("@eaDir/", StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(normalized)
        )
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (
            segments.Length == 0
            || segments.Any(segment =>
                segment == "." || segment == ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            )
        )
        {
            return false;
        }

        return AllowedImageExtensions.Contains(Path.GetExtension(normalized));
    }

    private static bool IsPathInsideDirectory(string candidate, string root)
    {
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            || string.Equals(candidate, root, StringComparison.Ordinal);
    }

    private static HtmlSanitizer CreateHtmlSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.AllowDataAttributes = false;
        return sanitizer;
    }

    private static string RewriteMarkdownImageSources(string html, string titlePath)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);

        foreach (var image in document.Images.ToArray())
        {
            var source = image.GetAttribute("src");
            if (string.IsNullOrWhiteSpace(source))
            {
                image.Remove();
                continue;
            }

            if (Uri.TryCreate(source, UriKind.Absolute, out var absoluteUri))
            {
                if (absoluteUri.Scheme is "http" or "https")
                {
                    image.SetAttribute("loading", "lazy");
                    continue;
                }

                image.Remove();
                continue;
            }

            if (!TryNormalizeImageReference(source, out var normalizedSource))
            {
                image.Remove();
                continue;
            }

            image.SetAttribute("loading", "lazy");
            image.SetAttribute("src", $"{titlePath}/{normalizedSource}");
        }

        return document.Body?.InnerHtml ?? string.Empty;
    }
}
