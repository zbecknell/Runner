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
}
