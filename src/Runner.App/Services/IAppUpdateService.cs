namespace Runner.App.Services;

public interface IAppUpdateService
{
    Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task ApplyUpdateAndRestartAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
