using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Runner.App.Services;

public sealed class AvaloniaRunnerRemovalConfirmation : IRunnerRemovalConfirmation
{
    private readonly Window _owner;

    public AvaloniaRunnerRemovalConfirmation(Window owner)
    {
        _owner = owner;
    }

    public async Task<bool> ConfirmRemoveAsync(
        string runnerName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = CreateDialog(runnerName);
        using var registration = cancellationToken.Register(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (dialog.IsVisible)
                {
                    dialog.Close(false);
                }
            });
        });

        return await dialog.ShowDialog<bool>(_owner);
    }

    private static Window CreateDialog(string runnerName)
    {
        var message = new TextBlock
        {
            Text = $"Remove \"{runnerName}\" from Runner?",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        var detail = new TextBlock
        {
            Text = "This removes the saved configuration entry. If the runner is active, its process will be stopped first.",
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 88
        };

        var removeButton = new Button
        {
            Content = "Remove",
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
                removeButton
            }
        };

        var content = new StackPanel
        {
            Spacing = 0,
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
            Title = "Remove runner",
            Content = content,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        cancelButton.Click += (_, _) => dialog.Close(false);
        removeButton.Click += (_, _) => dialog.Close(true);

        return dialog;
    }
}
