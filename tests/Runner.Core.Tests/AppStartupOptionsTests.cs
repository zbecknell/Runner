using Runner.App;

namespace Runner.Core.Tests;

public sealed class AppStartupOptionsTests
{
    [Fact]
    public void Parse_WhenSettingsArgumentPresent_EnablesOpenSettings()
    {
        var options = AppStartupOptions.Parse(["--settings"]);

        Assert.True(options.OpenSettings);
    }

    [Fact]
    public void Parse_WhenSettingsArgumentAbsent_DisablesOpenSettings()
    {
        var options = AppStartupOptions.Parse(["--other"]);

        Assert.False(options.OpenSettings);
    }

    [Fact]
    public void DetermineAction_ForInitialLaunchWithoutSettings_DoesNothing()
    {
        var action = AppStartupRequestBehavior.DetermineAction(
            AppStartupOptions.Parse([]),
            isSecondaryLaunch: false);

        Assert.Equal(AppStartupRequestAction.None, action);
    }

    [Fact]
    public void DetermineAction_ForSecondaryLaunchWithoutSettings_TogglesDashboard()
    {
        var action = AppStartupRequestBehavior.DetermineAction(
            AppStartupOptions.Parse([]),
            isSecondaryLaunch: true);

        Assert.Equal(AppStartupRequestAction.ToggleDashboard, action);
    }

    [Fact]
    public void DetermineAction_WhenSettingsArgumentPresent_OpensSettings()
    {
        var initialAction = AppStartupRequestBehavior.DetermineAction(
            AppStartupOptions.Parse(["--settings"]),
            isSecondaryLaunch: false);
        var secondaryAction = AppStartupRequestBehavior.DetermineAction(
            AppStartupOptions.Parse(["--settings"]),
            isSecondaryLaunch: true);

        Assert.Equal(AppStartupRequestAction.OpenSettings, initialAction);
        Assert.Equal(AppStartupRequestAction.OpenSettings, secondaryAction);
    }
}
