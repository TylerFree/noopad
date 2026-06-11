namespace Nopad.Services;

public interface IFileDialogService
{
    Task<string?> OpenFileAsync();
    Task<string?> SaveFileAsync(string? currentPath, string? suggestedName);
}
