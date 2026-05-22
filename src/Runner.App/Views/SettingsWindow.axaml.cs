using Avalonia;
using Avalonia.Controls;
using Runner.Core.Config;
using Runner.App.Services;
using Runner.App.ViewModels;

namespace Runner.App.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsCloseConfirmation _settingsCloseConfirmation;
    private bool _closeConfirmed;
    private bool _closePromptStarted;
    private bool _isOpened;
    private bool _isApplyingPlacement;
    private bool _placementApplied;
    private WindowPlacement? _closingPlacement;
    private WindowPlacement? _lastNormalPlacement;
    private WindowPlacement? _pendingPlacement;

    public SettingsWindow()
        : this(null)
    {
    }

    public SettingsWindow(ISettingsCloseConfirmation? settingsCloseConfirmation)
    {
        InitializeComponent();
        _settingsCloseConfirmation = settingsCloseConfirmation ?? new AvaloniaSettingsCloseConfirmation(this);
        PositionChanged += (_, _) => CaptureNormalPlacement();
        Resized += (_, _) => CaptureNormalPlacement();
    }

    public void CloseWithoutConfirmation()
    {
        _closeConfirmed = true;
        Close();
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
        if (_closeConfirmed)
        {
            _closingPlacement = GetCurrentWindowPlacement();
            base.OnClosing(e);
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsSettingsDirty)
        {
            _closingPlacement = GetCurrentWindowPlacement();
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);
        ConfirmCloseAsync(viewModel);
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.SaveSettingsWindowPlacementAsync(_closingPlacement ?? GetCurrentWindowPlacement());
        }
    }

    private async void ConfirmCloseAsync(MainWindowViewModel viewModel)
    {
        if (_closePromptStarted)
        {
            return;
        }

        _closePromptStarted = true;

        try
        {
            var action = await _settingsCloseConfirmation.ConfirmCloseAsync();

            if (action == SettingsCloseAction.Cancel)
            {
                return;
            }

            if (action == SettingsCloseAction.Save)
            {
                await viewModel.SaveConfigCommand.ExecuteAsync(null);
            }
            else if (action == SettingsCloseAction.Discard)
            {
                viewModel.DiscardSettingsChanges();
            }

            _closeConfirmed = true;
            Close();
        }
        finally
        {
            _closePromptStarted = false;
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
