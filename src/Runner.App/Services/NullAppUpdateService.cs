namespace Runner.App.Services;

public sealed class NullAppUpdateService : IAppUpdateService
{
    public static NullAppUpdateService Instance { get; } = new();

    private NullAppUpdateService()
    {
    }

    public Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AppUpdateCheckResult.Unsupported("Updates are unavailable in this context."));
    }

    public Task ApplyUpdateAndRestartAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("No update is available.");
    }
}
