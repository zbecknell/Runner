namespace Runner.App.Services;

public interface IWorkingDirectoryPicker
{
    Task<string?> PickWorkingDirectoryAsync(string? currentPath, CancellationToken cancellationToken = default);
}
