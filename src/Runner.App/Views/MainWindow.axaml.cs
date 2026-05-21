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

    public MainWindow()
    {
        InitializeComponent();
        PositionChanged += (_, _) => CaptureNormalPlacement();
        Resized += (_, _) => CaptureNormalPlacement();
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
}
