using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Renderers;
using Markdig.Renderers.Html;

public sealed class GalleryContainerExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline) { }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer && !htmlRenderer.ObjectRenderers.Contains<GalleryContainerRenderer>())
            htmlRenderer.ObjectRenderers.Insert(0, new GalleryContainerRenderer());
    }
}

public sealed class GalleryContainerRenderer : HtmlObjectRenderer<CustomContainer>
{
    protected override void Write(HtmlRenderer renderer, CustomContainer obj)
    {
        renderer.EnsureLine();

        if (renderer.EnableHtmlForBlock)
        {
            if (string.Equals(obj.Info, "gallery", StringComparison.OrdinalIgnoreCase))
                renderer.Write("<div data-gallery=\"true\">");
            else
                renderer.Write("<div>");
        }

        renderer.WriteChildren(obj);

        if (renderer.EnableHtmlForBlock)
            renderer.WriteLine("</div>");
    }
}
