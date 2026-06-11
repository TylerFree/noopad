using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Nopad.Services;

public class FileDialogService : IFileDialogService
{
    private readonly Window _owner;

    public FileDialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> OpenFileAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(string? currentPath, string? suggestedName)
    {
        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File",
            SuggestedFileName = suggestedName ?? "untitled.txt"
        });
        return file?.TryGetLocalPath();
    }
}
