using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nopad.ViewModels;

public partial class SearchReplacePanelViewModel : ObservableObject
{
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _replaceText = string.Empty;
    [ObservableProperty] private bool _matchCase;
    [ObservableProperty] private bool _wholeWord;
    [ObservableProperty] private bool _useRegex;
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _showReplace;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public event Action? FindNextRequested;
    public event Action? FindPreviousRequested;
    public event Action? ReplaceCurrentRequested;
    public event Action? ReplaceAllRequested;
    public event Action? CloseRequested;

    [RelayCommand]
    private void FindNext() => FindNextRequested?.Invoke();

    [RelayCommand]
    private void FindPrevious() => FindPreviousRequested?.Invoke();

    [RelayCommand]
    private void ReplaceCurrent() => ReplaceCurrentRequested?.Invoke();

    [RelayCommand]
    private void ReplaceAll() => ReplaceAllRequested?.Invoke();

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        CloseRequested?.Invoke();
    }
}
