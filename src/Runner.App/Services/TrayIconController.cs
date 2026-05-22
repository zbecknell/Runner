using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Windows.Input;
using Runner.App.Views;

namespace Runner.App.Services;

public sealed class TrayIconController : INotifyPropertyChanged
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly MainWindow _mainWindow;
    private bool _exitStarted;

    public TrayIconController(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow mainWindow)
    {
        _desktop = desktop;
        _mainWindow = mainWindow;
        ToggleWindowCommand = new RelayCommand(ToggleWindow);
        ExitCommand = new AsyncRelayCommand(ExitAsync);
        _mainWindow.TrayVisibilityChanged += OnTrayVisibilityChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ToggleWindowHeader => _mainWindow.IsShownFromTray
        ? "Hide Runner"
        : "Show Runner";

    public ICommand ToggleWindowCommand { get; }

    public IAsyncRelayCommand ExitCommand { get; }

    public void ToggleWindow()
    {
        if (_exitStarted)
        {
            return;
        }

        _mainWindow.ToggleTrayVisibility();
    }

    public async Task ExitAsync()
    {
        if (_exitStarted)
        {
            return;
        }

        _exitStarted = true;
        await _mainWindow.ExitApplicationAsync();
        _desktop.Shutdown();
    }

    private void OnTrayVisibilityChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleWindowHeader)));
    }
}
