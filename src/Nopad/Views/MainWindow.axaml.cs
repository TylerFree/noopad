using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Noopad.Services;
using Noopad.ViewModels;

namespace Noopad.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;
    private bool _singleInstanceServerStarted;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _vm = DataContext as MainWindowViewModel;
        if (_vm == null) return;

        _vm.FileDialog = new FileDialogService(this);

        _vm.SaveDialogHandler = async (tab) =>
        {
            var dialog = new SaveConfirmDialog(tab.Title);
            return await dialog.ShowDialog<bool?>(this);
        };

        _vm.CreateMissingFileHandler = async (path) =>
        {
            var dialog = new CreateFileDialog(path);
            return await dialog.ShowDialog<bool>(this);
        };

        _vm.ShowSettingsHandler = async () =>
        {
            if (_vm?.Settings is IUserSettingsService svc)
            {
                var dialog = new SettingsDialog(svc);
                var result = await dialog.ShowDialog<bool?>(this);
                if (result == true)
                {
                    _vm.ApplySettingsToAllTabs();
                    FindEditorView()?.ApplyFontSettings(svc.Settings);
                }
            }
        };

        _vm.SelectTextRequested += (start, length) =>
        {
            FindEditorView()?.SelectText(start, length);
        };

        WireTabStrip();
        WireMenuItems();

        await _vm.InitializeAsync();
        StartSingleInstanceServer();
    }

    private void StartSingleInstanceServer()
    {
        if (_singleInstanceServerStarted || App.SingleInstanceCoordinator == null)
            return;

        _singleInstanceServerStarted = true;
        App.SingleInstanceCoordinator.StartServer(async args =>
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                BringToFront();
                if (_vm != null)
                    await _vm.OpenStartupFilePathsAsync(args, promptToCreate: true);
            });
        });
    }

    private void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        Focus();
    }

    private void WireTabStrip()
    {
        var tabStrip = this.FindControl<ItemsControl>("TabStrip");
        if (tabStrip == null) return;

        tabStrip.AddHandler(Button.ClickEvent, (object? sender, RoutedEventArgs e) =>
        {
            if (e.Source is Button btn)
            {
                if (btn.Name == "CloseTabBtn")
                {
                    var tabVm = btn.DataContext as EditorTabViewModel;
                    if (_vm != null) _ = _vm.CloseTabCommand.ExecuteAsync(tabVm);
                    e.Handled = true;
                }
                else if (btn.Name == "TabButton")
                {
                    if (_vm != null && btn.DataContext is EditorTabViewModel tabVm)
                        _vm.ActiveTab = tabVm;
                    e.Handled = true;
                }
            }
        });
    }

    private void WireMenuItems()
    {
        if (_vm == null) return;

        var menuExit = this.FindControl<MenuItem>("MenuExit");
        if (menuExit != null) menuExit.Click += (_, _) => Close();

        var menuAbout = this.FindControl<MenuItem>("MenuAbout");
        if (menuAbout != null) menuAbout.Click += async (_, _) =>
        {
            var dlg = new AboutDialog();
            await dlg.ShowDialog(this);
        };

        var menuFindNext = this.FindControl<MenuItem>("MenuFindNext");
        if (menuFindNext != null) menuFindNext.Click += (_, _) => _vm.SearchPanel.FindNextCommand.Execute(null);

        var menuFindPrev = this.FindControl<MenuItem>("MenuFindPrev");
        if (menuFindPrev != null) menuFindPrev.Click += (_, _) => _vm.SearchPanel.FindPreviousCommand.Execute(null);

        var menuUndo = this.FindControl<MenuItem>("MenuUndo");
        if (menuUndo != null) menuUndo.Click += (_, _) => FindEditorView()?.Undo();

        var menuRedo = this.FindControl<MenuItem>("MenuRedo");
        if (menuRedo != null) menuRedo.Click += (_, _) => FindEditorView()?.Redo();

        var menuSelectAll = this.FindControl<MenuItem>("MenuSelectAll");
        if (menuSelectAll != null) menuSelectAll.Click += (_, _) => FindEditorView()?.SelectAll();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm == null) return;

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Tab)
        {
            _vm.NextTab(); e.Handled = true;
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Tab)
        {
            _vm.PreviousTab(); e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.F3)
        {
            _vm.SearchPanel.FindNextCommand.Execute(null); e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.Shift && e.Key == Key.F3)
        {
            _vm.SearchPanel.FindPreviousCommand.Execute(null); e.Handled = true;
        }
    }

    private EditorDocumentView? FindEditorView()
    {
        return FindDescendant<EditorDocumentView>(this);
    }

    private static T? FindDescendant<T>(Avalonia.Visual root) where T : Avalonia.Visual
    {
        if (root is T match) return match;
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(root))
        {
            var found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_vm != null)
            await _vm.SaveRecoveryAsync();
        base.OnClosing(e);
    }
}