namespace Noopad.Services;

public interface IMarkdownPreviewService
{
    string RenderToHtml(string markdown);
}
