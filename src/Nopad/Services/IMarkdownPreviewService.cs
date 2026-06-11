namespace Nopad.Services;

public interface IMarkdownPreviewService
{
    string RenderToHtml(string markdown);
}
