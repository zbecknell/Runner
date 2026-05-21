namespace Runner.App.Services;

public sealed record AppUpdateCheckResult(
    bool IsSupported,
    bool IsAvailable,
    bool IsDownloaded,
    string? CurrentVersion,
    string? AvailableVersion,
    string Message)
{
    public static AppUpdateCheckResult Unsupported(string message)
    {
        return new AppUpdateCheckResult(false, false, false, null, null, message);
    }

    public static AppUpdateCheckResult UpToDate(string? currentVersion)
    {
        return new AppUpdateCheckResult(true, false, false, currentVersion, null, "Runner is up to date.");
    }

    public static AppUpdateCheckResult Available(
        string? currentVersion,
        string availableVersion,
        bool isDownloaded)
    {
        var message = isDownloaded
            ? $"Update {availableVersion} is ready to apply."
            : $"Update {availableVersion} is available.";

        return new AppUpdateCheckResult(true, true, isDownloaded, currentVersion, availableVersion, message);
    }
}
