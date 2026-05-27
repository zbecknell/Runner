using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Windows.Input;
using Runner.App.ViewModels;
using Runner.Core.Config;

namespace Runner.App.Views;

public partial class MainWindow : Window
{
    private const double ResizeDragExclusionMargin = 8;
    private bool _closeAfterCleanup;
    private bool _isOpened;
    private bool _placementApplied;
    private Task? _cleanupTask;
    private WindowPlacement? _lastNormalPlacement;
    private WindowPlacement? _pendingPlacement;
    private RunnerDetailsWindow? _detailsWindow;
    private SettingsWindow? _settingsWindow;
    private MainWindowViewModel? _viewModel;

    public event EventHandler? TrayVisibilityChanged;

    public MainWindow()
    {
        InitializeComponent();
        PositionChanged += (_, _) => CaptureNormalPlacement();
        Resized += (_, _) => CaptureNormalPlacement();
        DataContextChanged += (_, _) => SetViewModel(DataContext as MainWindowViewModel);
        AddHandler(PointerPressedEvent, OnDashboardPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(ContextRequestedEvent, OnRunnerContextRequested, RoutingStrategies.Bubble);
    }

    public bool IsShownFromTray => IsVisible && WindowState != WindowState.Minimized;

    public void OpenSettingsWindow()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        if (_viewModel is null)
        {
            return;
        }

        _settingsWindow = new SettingsWindow
        {
            DataContext = _viewModel
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.ApplyWindowPlacement(_viewModel.SettingsWindowPlacement);
        _settingsWindow.Show(this);
        _settingsWindow.Activate();
    }

    public void OpenRunnerDetailsWindow(RunnerViewModel runner)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_detailsWindow is { IsVisible: true }
            && _detailsWindow.DataContext is RunnerDetailsViewModel detailsViewModel)
        {
            detailsViewModel.Runner = runner;
            _detailsWindow.Activate();
            return;
        }

        _detailsWindow = new RunnerDetailsWindow
        {
            DataContext = new RunnerDetailsViewModel(_viewModel, runner)
        };
        _detailsWindow.Closed += (_, _) => _detailsWindow = null;
        _detailsWindow.ApplyWindowPlacement(_viewModel.DetailsWindowPlacement);
        _detailsWindow.Show(this);
        _detailsWindow.Activate();
    }

    private void ToggleSettingsWindow()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Close();
            return;
        }

        OpenSettingsWindow();
    }

    public void ApplyWindowPlacement(WindowPlacement? placement)
    {
        if (placement is null || _placementApplied)
        {
            return;
        }

        if (!_isOpened)
        {
            _pendingPlacement = placement;
            return;
        }

        _placementApplied = true;
        var width = ClampDimension(placement.Width, MinWidth);
        var height = ClampDimension(placement.Height, MinHeight);
        Width = width;
        Height = height;

        var useSavedPosition = IsPlacementOnScreen(placement, width, height);

        if (useSavedPosition)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(placement.X, placement.Y);
        }

        _lastNormalPlacement = new WindowPlacement
        {
            X = useSavedPosition ? placement.X : Position.X,
            Y = useSavedPosition ? placement.Y : Position.Y,
            Width = width,
            Height = height,
            IsMaximized = false
        };

        if (placement.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    public void ToggleTrayVisibility()
    {
        if (IsShownFromTray)
        {
            HideToTray();
        }
        else
        {
            ShowFromTray();
        }
    }

    public void HideToTray()
    {
        if (_cleanupTask is not null)
        {
            return;
        }

        CaptureNormalPlacement();
        ShowInTaskbar = false;
        Hide();
        OnTrayVisibilityChanged();
    }

    public void ShowFromTray()
    {
        if (_cleanupTask is not null)
        {
            return;
        }

        ShowInTaskbar = true;

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        OnTrayVisibilityChanged();
    }

    public Task ExitApplicationAsync()
    {
        return BeginCleanupAndCloseAsync();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _isOpened = true;
        OnTrayVisibilityChanged();

        if (_pendingPlacement is { } placement)
        {
            _pendingPlacement = null;
            ApplyWindowPlacement(placement);
            return;
        }

        CaptureNormalPlacement();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty || change.Property == WindowStateProperty)
        {
            OnTrayVisibilityChanged();
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_closeAfterCleanup)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);
        HideToTray();
    }

    private Task BeginCleanupAndCloseAsync()
    {
        if (_cleanupTask is not null)
        {
            return _cleanupTask;
        }

        _cleanupTask = CleanupAndCloseAsync();
        return _cleanupTask;
    }

    private async Task CleanupAndCloseAsync()
    {
        IsEnabled = false;
        CloseDetailsWindowForShutdown();
        CloseSettingsWindowForShutdown();

        try
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                SetViewModel(null);
                await viewModel.SaveWindowPlacementAsync(GetCurrentWindowPlacement());
                await viewModel.DisposeAsync();
            }
        }
        finally
        {
            _closeAfterCleanup = true;
            Close();
        }
    }

    private void CloseSettingsWindowForShutdown()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.CloseWithoutConfirmation();
        _settingsWindow = null;
    }

    private void CloseDetailsWindowForShutdown()
    {
        if (_detailsWindow is null)
        {
            return;
        }

        _detailsWindow.Close();
        _detailsWindow = null;
    }

    private void CaptureNormalPlacement()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _lastNormalPlacement = new WindowPlacement
        {
            X = Position.X,
            Y = Position.Y,
            Width = ClampDimension(Width, MinWidth),
            Height = ClampDimension(Height, MinHeight),
            IsMaximized = false
        };
    }

    private WindowPlacement GetCurrentWindowPlacement()
    {
        if (WindowState == WindowState.Normal)
        {
            CaptureNormalPlacement();
        }

        var placement = _lastNormalPlacement ?? new WindowPlacement
        {
            X = Position.X,
            Y = Position.Y,
            Width = ClampDimension(Width, MinWidth),
            Height = ClampDimension(Height, MinHeight),
            IsMaximized = false
        };

        return new WindowPlacement
        {
            X = placement.X,
            Y = placement.Y,
            Width = ClampDimension(placement.Width, MinWidth),
            Height = ClampDimension(placement.Height, MinHeight),
            IsMaximized = WindowState == WindowState.Maximized
        };
    }

    private bool IsPlacementOnScreen(WindowPlacement placement, double width, double height)
    {
        var bounds = new PixelRect(
            placement.X,
            placement.Y,
            Math.Max(1, (int)Math.Round(width * DesktopScaling)),
            Math.Max(1, (int)Math.Round(height * DesktopScaling)));

        return Screens.All.Any(screen => screen.WorkingArea.Intersects(bounds));
    }

    private static double ClampDimension(double value, double minimum)
    {
        return double.IsFinite(value)
            ? Math.Max(minimum, value)
            : minimum;
    }

    private void OnDashboardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_cleanupTask is not null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);

        if (e.ClickCount > 1)
        {
            if (point.Properties.IsLeftButtonPressed
                && !IsInteractiveDragSource(e.Source)
                && TryFindRunnerDashboardItem(e.Source) is { } runner)
            {
                _viewModel?.OpenRunnerDetailsCommand.Execute(runner);
                e.Handled = true;
            }

            return;
        }

        if (!point.Properties.IsLeftButtonPressed
            || IsResizeDragStart(point.Position)
            || TryFindVisualAncestor<ListBoxItem>(e.Source) is not null
            || IsInteractiveDragSource(e.Source))
        {
            return;
        }

        BeginMoveDrag(e);
    }

    private bool IsResizeDragStart(Point position)
    {
        if (!CanResize || WindowState != WindowState.Normal)
        {
            return false;
        }

        return position.X <= ResizeDragExclusionMargin
            || position.Y <= ResizeDragExclusionMargin
            || position.X >= ClientSize.Width - ResizeDragExclusionMargin
            || position.Y >= ClientSize.Height - ResizeDragExclusionMargin;
    }

    private static bool IsInteractiveDragSource(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Button
                or TextBox
                or ComboBox
                or ScrollBar
                or Thumb
                or MenuItem)
            {
                return true;
            }
        }

        return false;
    }

    private void OnRunnerContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_viewModel is null || TryFindVisualAncestor<MenuItem>(e.Source) is not null)
        {
            return;
        }

        var listBoxItem = TryFindVisualAncestor<ListBoxItem>(e.Source);
        if (listBoxItem?.DataContext is not RunnerDashboardItemViewModel runnerItem)
        {
            return;
        }

        var runner = runnerItem.Runner;
        _viewModel.SelectedRunner = runner;
        var menu = new ContextMenu
        {
            ItemsSource = CreateRunnerMenuItems(_viewModel, runner)
        };

        e.Handled = true;
        menu.Open(listBoxItem);
    }

    private static Control[] CreateRunnerMenuItems(
        MainWindowViewModel viewModel,
        RunnerViewModel runner)
    {
        if (runner.IsBuildOnly)
        {
            return
            [
                CreateRunnerMenuItem("Details", viewModel.OpenRunnerDetailsCommand, runner),
                new Separator(),
                CreateRunnerMenuItem("Clean", viewModel.CleanRunnerCommand, runner),
                CreateRunnerMenuItem("Build", viewModel.BuildRunnerCommand, runner)
            ];
        }

        if (runner.IsCustomCommands)
        {
            return
            [
                CreateRunnerMenuItem("Details", viewModel.OpenRunnerDetailsCommand, runner),
                new Separator(),
                CreateRunnerMenuItem("Run", viewModel.StartRunnerCommand, runner),
                CreateRunnerMenuItem("Stop", viewModel.StopRunnerCommand, runner),
                new Separator(),
                CreateRunnerMenuItem("Clean", viewModel.CleanRunnerCommand, runner),
                CreateRunnerMenuItem("Build", viewModel.BuildRunnerCommand, runner)
            ];
        }

        return
        [
            CreateRunnerMenuItem("Details", viewModel.OpenRunnerDetailsCommand, runner),
            new Separator(),
            CreateRunnerMenuItem("Run", viewModel.StartRunnerCommand, runner),
            CreateRunnerMenuItem("Restart", viewModel.RestartRunnerCommand, runner),
            CreateRunnerMenuItem("Stop", viewModel.StopRunnerCommand, runner),
            new Separator(),
            CreateRunnerMenuItem("Clean", viewModel.CleanRunnerCommand, runner),
            CreateRunnerMenuItem("Build", viewModel.BuildRunnerCommand, runner)
        ];
    }

    private static MenuItem CreateRunnerMenuItem(
        string header,
        ICommand command,
        RunnerViewModel runner)
    {
        return new MenuItem
        {
            Header = header,
            Command = command,
            CommandParameter = runner
        };
    }

    private static RunnerViewModel? TryFindRunnerDashboardItem(object? source)
    {
        return TryFindVisualAncestor<ListBoxItem>(source)?.DataContext switch
        {
            RunnerDashboardItemViewModel item => item.Runner,
            RunnerViewModel runner => runner,
            _ => null
        };
    }

    private static T? TryFindVisualAncestor<T>(object? source)
        where T : Visual
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is T match)
            {
                return match;
            }
        }

        return null;
    }

    private void SetViewModel(MainWindowViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.SettingsOpenRequested -= OnSettingsOpenRequested;
            _viewModel.RunnerDetailsOpenRequested -= OnRunnerDetailsOpenRequested;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.SettingsOpenRequested += OnSettingsOpenRequested;
            _viewModel.RunnerDetailsOpenRequested += OnRunnerDetailsOpenRequested;
        }
    }

    private void OnSettingsOpenRequested(object? sender, EventArgs e)
    {
        OpenSettingsWindow();
    }

    private void OnRunnerDetailsOpenRequested(object? sender, RunnerViewModel runner)
    {
        OpenRunnerDetailsWindow(runner);
    }

    private void OnSettingsButtonClick(object? sender, RoutedEventArgs e)
    {
        ToggleSettingsWindow();
    }

    private void OnTrayVisibilityChanged()
    {
        TrayVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
