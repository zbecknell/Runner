namespace Runner.App;

public static class AppStartupRequestBehavior
{
    public static AppStartupRequestAction DetermineAction(
        AppStartupOptions options,
        bool isSecondaryLaunch)
    {
        if (options.OpenSettings)
        {
            return AppStartupRequestAction.OpenSettings;
        }

        return isSecondaryLaunch
            ? AppStartupRequestAction.ToggleDashboard
            : AppStartupRequestAction.None;
    }
}
