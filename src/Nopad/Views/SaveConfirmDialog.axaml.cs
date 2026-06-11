using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Noopad.Views;

public partial class SaveConfirmDialog : Window
{
    public SaveConfirmDialog() : this("Untitled") { }

    public SaveConfirmDialog(string fileName)
    {
        InitializeComponent();
        var msg = this.FindControl<TextBlock>("MessageText");
        if (msg != null) msg.Text = $"Do you want to save changes to \"{fileName}\"?";

        var saveBtn = this.FindControl<Button>("SaveBtn");
        var discardBtn = this.FindControl<Button>("DiscardBtn");
        var cancelBtn = this.FindControl<Button>("CancelBtn");

        if (saveBtn != null) saveBtn.Click += (_, _) => Close(true);
        if (discardBtn != null) discardBtn.Click += (_, _) => Close(false);
        if (cancelBtn != null) cancelBtn.Click += (_, _) => Close(null);
    }
}
