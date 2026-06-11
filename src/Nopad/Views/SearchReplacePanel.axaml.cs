using Avalonia.Controls;
using Avalonia.Input;

namespace Nopad.Views;

public partial class SearchReplacePanel : UserControl
{
    public SearchReplacePanel()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Focus the search box when panel is shown
        var box = this.FindControl<TextBox>("SearchBox");
        box?.Focus();
    }
}
