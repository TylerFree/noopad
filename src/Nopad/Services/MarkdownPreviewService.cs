using Markdig;

namespace Noopad.Services;

public class MarkdownPreviewService : IMarkdownPreviewService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownPreviewService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string RenderToHtml(string markdown)
    {
        var body = Markdig.Markdown.ToHtml(markdown, _pipeline);
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8""/>
<style>
body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
       margin: 16px; line-height: 1.6; color: #d4d4d4; background: #1e1e1e; }}
h1,h2,h3,h4,h5,h6 {{ color: #e8e8e8; border-bottom: 1px solid #444; padding-bottom: 4px; }}
code {{ background: #2d2d2d; padding: 2px 4px; border-radius: 3px; font-family: Consolas, monospace; }}
pre {{ background: #2d2d2d; padding: 12px; border-radius: 4px; overflow-x: auto; }}
pre code {{ background: none; padding: 0; }}
blockquote {{ border-left: 4px solid #555; padding-left: 12px; color: #aaa; margin: 0; }}
a {{ color: #569cd6; }}
table {{ border-collapse: collapse; width: 100%; }}
th, td {{ border: 1px solid #444; padding: 6px 12px; }}
th {{ background: #2d2d2d; }}
img {{ max-width: 100%; }}
</style>
</head>
<body>{body}</body>
</html>";
    }
}
