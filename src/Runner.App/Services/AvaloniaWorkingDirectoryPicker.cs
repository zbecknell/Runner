using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Runner.App.Services;

public sealed class AvaloniaWorkingDirectoryPicker : IWorkingDirectoryPicker
{
    private readonly Window _window;

    public AvaloniaWorkingDirectoryPicker(Window window)
    {
        _window = window;
    }

    public async Task<string?> PickWorkingDirectoryAsync(
        string? currentPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = new FolderPickerOpenOptions
        {
            Title = "Choose working folder",
            AllowMultiple = false
        };

        if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
        {
            options.SuggestedStartLocation =
                await _window.StorageProvider.TryGetFolderFromPathAsync(currentPath);
        }

        var folders = await _window.StorageProvider.OpenFolderPickerAsync(options);
        cancellationToken.ThrowIfCancellationRequested();

        var selectedFolder = folders.FirstOrDefault();
        return selectedFolder?.TryGetLocalPath() ?? selectedFolder?.Path.LocalPath;
    }
}
