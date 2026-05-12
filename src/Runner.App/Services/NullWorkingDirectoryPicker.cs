namespace Runner.App.Services;

public sealed class NullWorkingDirectoryPicker : IWorkingDirectoryPicker
{
    public static NullWorkingDirectoryPicker Instance { get; } = new();

    private NullWorkingDirectoryPicker()
    {
    }

    public Task<string?> PickWorkingDirectoryAsync(string? currentPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
