using Avalonia;
using Avalonia.Controls;
using Runner.App.ViewModels;
using Runner.Core.Config;

namespace Runner.App.Views;

public partial class MainWindow : Window
{
    private bool _closeAfterCleanup;
    private bool _cleanupStarted;
    private bool _isOpened;
    private bool _placementApplied;
    private WindowPlacement? _lastNormalPlacement;
    private WindowPlacement? _pendingPlacement;
    private SettingsWindow? _settingsWindow;
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        PositionChanged += (_, _) => CaptureNormalPlacement();
        Resized += (_, _) => CaptureNormalPlacement();
        DataContextChanged += (_, _) => SetViewModel(DataContext as MainWindowViewModel);
    }

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
        _settingsWindow.Show(this);
        _settingsWindow.Activate();
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

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _isOpened = true;

        if (_pendingPlacement is { } placement)
        {
            _pendingPlacement = null;
            ApplyWindowPlacement(placement);
            return;
        }

        CaptureNormalPlacement();
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
        BeginCleanupAndClose();
    }

    private async void BeginCleanupAndClose()
    {
        if (_cleanupStarted)
        {
            return;
        }

        _cleanupStarted = true;
        IsEnabled = false;

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

    private void SetViewModel(MainWindowViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.SettingsOpenRequested -= OnSettingsOpenRequested;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.SettingsOpenRequested += OnSettingsOpenRequested;
        }
    }

    private void OnSettingsOpenRequested(object? sender, EventArgs e)
    {
        OpenSettingsWindow();
    }
}
