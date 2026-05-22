using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Runner.App.ViewModels;
using Runner.Core.Config;

namespace Runner.App.Views;

public partial class RunnerDetailsWindow : Window
{
    private bool _isOpened;
    private bool _isApplyingPlacement;
    private bool _placementApplied;
    private WindowPlacement? _closingPlacement;
    private WindowPlacement? _lastNormalPlacement;
    private WindowPlacement? _pendingPlacement;

    public RunnerDetailsWindow()
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
        var useSavedPosition = IsPlacementOnScreen(placement, width, height);

        _isApplyingPlacement = true;

        try
        {
            if (useSavedPosition)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint(placement.X, placement.Y);
            }

            Width = width;
            Height = height;

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
        finally
        {
            _isApplyingPlacement = false;
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
        _closingPlacement = GetCurrentWindowPlacement();
        base.OnClosing(e);
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (DataContext is RunnerDetailsViewModel viewModel)
        {
            viewModel.Dispose();
            await viewModel.Dashboard.SaveDetailsWindowPlacementAsync(
                _closingPlacement ?? GetCurrentWindowPlacement());
        }
    }

    private async void OnCopyLogsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is RunnerDetailsViewModel viewModel && Clipboard is not null)
        {
            await Clipboard.SetTextAsync(viewModel.LogText);
        }
    }

    private void CaptureNormalPlacement()
    {
        if (_isApplyingPlacement || WindowState != WindowState.Normal)
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
