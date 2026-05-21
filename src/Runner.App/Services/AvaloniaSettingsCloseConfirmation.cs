using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Runner.App.Services;

public sealed class AvaloniaSettingsCloseConfirmation : ISettingsCloseConfirmation
{
    private readonly Window _owner;

    public AvaloniaSettingsCloseConfirmation(Window owner)
    {
        _owner = owner;
    }

    public async Task<SettingsCloseAction> ConfirmCloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = CreateDialog(_owner.Topmost);
        using var registration = cancellationToken.Register(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (dialog.IsVisible)
                {
                    dialog.Close(SettingsCloseAction.Cancel);
                }
            });
        });

        return await dialog.ShowDialog<SettingsCloseAction>(_owner);
    }

    private static Window CreateDialog(bool topmost)
    {
        var message = new TextBlock
        {
            Text = "Save changes before closing Settings?",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        var detail = new TextBlock
        {
            Text = "You have unsaved runner setting changes.",
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 88
        };

        var discardButton = new Button
        {
            Content = "Discard",
            MinWidth = 88
        };

        var saveButton = new Button
        {
            Content = "Save",
            MinWidth = 88
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 20, 0, 0),
            Children =
            {
                cancelButton,
                discardButton,
                saveButton
            }
        };

        var content = new StackPanel
        {
            Margin = new Avalonia.Thickness(18),
            Width = 430,
            Children =
            {
                message,
                detail,
                buttons
            }
        };

        var dialog = new Window
        {
            Title = "Unsaved settings",
            Content = content,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Topmost = topmost
        };

        cancelButton.Click += (_, _) => dialog.Close(SettingsCloseAction.Cancel);
        discardButton.Click += (_, _) => dialog.Close(SettingsCloseAction.Discard);
        saveButton.Click += (_, _) => dialog.Close(SettingsCloseAction.Save);

        return dialog;
    }
}
