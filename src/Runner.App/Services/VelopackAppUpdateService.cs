using Velopack;
using Velopack.Sources;

namespace Runner.App.Services;

public sealed class VelopackAppUpdateService : IAppUpdateService
{
    public const string RepositoryUrl = "https://github.com/zbecknell/Runner";

    private readonly UpdateManager _updateManager;
    private UpdateInfo? _pendingUpdate;
    private VelopackAsset? _pendingRestart;

    public VelopackAppUpdateService()
        : this(new UpdateManager(new GithubSource(RepositoryUrl, string.Empty, prerelease: false)))
    {
    }

    internal VelopackAppUpdateService(UpdateManager updateManager)
    {
        _updateManager = updateManager;
    }

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        _pendingUpdate = null;
        _pendingRestart = null;

        if (!_updateManager.IsInstalled)
        {
            return AppUpdateCheckResult.Unsupported("Updates are available after installing Runner from a release package.");
        }

        _pendingRestart = _updateManager.UpdatePendingRestart;
        if (_pendingRestart is not null)
        {
            return AppUpdateCheckResult.Available(
                GetCurrentVersion(),
                _pendingRestart.Version.ToString(),
                isDownloaded: true);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var update = await _updateManager.CheckForUpdatesAsync();
        if (update is null)
        {
            return AppUpdateCheckResult.UpToDate(GetCurrentVersion());
        }

        _pendingUpdate = update;

        return AppUpdateCheckResult.Available(
            GetCurrentVersion(),
            update.TargetFullRelease.Version.ToString(),
            isDownloaded: false);
    }

    public async Task ApplyUpdateAndRestartAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_pendingRestart is not null)
        {
            _updateManager.ApplyUpdatesAndRestart(_pendingRestart);
            return;
        }

        if (_pendingUpdate is null)
        {
            throw new InvalidOperationException("Check for updates before applying an update.");
        }

        await _updateManager.DownloadUpdatesAsync(
            _pendingUpdate,
            progress is null ? null : value => progress.Report(value),
            cancellationToken);

        _updateManager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
    }

    private string? GetCurrentVersion()
    {
        return _updateManager.CurrentVersion?.ToString();
    }
}
