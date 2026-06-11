using Avalonia.Controls;

namespace Noopad.Views;

public partial class CreateFileDialog : Window
{
    public CreateFileDialog() : this("Untitled") { }

    public CreateFileDialog(string filePath)
    {
        InitializeComponent();

        var message = this.FindControl<TextBlock>("MessageText");
        if (message != null)
            message.Text = $"\"{filePath}\" does not exist. Do you want to create it?";

        var createBtn = this.FindControl<Button>("CreateBtn");
        var cancelBtn = this.FindControl<Button>("CancelBtn");

        if (createBtn != null) createBtn.Click += (_, _) => Close(true);
        if (cancelBtn != null) cancelBtn.Click += (_, _) => Close(false);
    }
}
