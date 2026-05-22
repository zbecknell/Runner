using Runner.Core.Config;
using Runner.Core.Runners;

namespace Runner.Core.Tests;

public sealed class RunnerConfigStoreTests
{
    [Fact]
    public async Task SaveAsyncAndLoadAsync_RoundTripConfig()
    {
        using var directory = TempDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        var store = new RunnerConfigStore(path);

        await store.SaveAsync(new RunnerConfig
        {
            AlwaysOnTop = true,
            ShowNewestLogsFirst = false,
            ShowProjectPaths = false,
            RunnerOpacity = 0.45,
            WindowPlacement = new WindowPlacement
            {
                X = 120,
                Y = 80,
                Width = 1440,
                Height = 900,
                IsMaximized = true
            },
            SettingsWindowPlacement = new WindowPlacement
            {
                X = 240,
                Y = 160,
                Width = 1000,
                Height = 720,
                IsMaximized = false
            },
            DetailsWindowPlacement = new WindowPlacement
            {
                X = 300,
                Y = 220,
                Width = 960,
                Height = 640,
                IsMaximized = true
            },
            Runners =
            [
                new RunnerDefinition
                {
                    Id = "runner-1",
                    DisplayName = "API",
                    Type = RunnerType.DotNetProject,
                    WorkingDirectory = "C:\\src\\api",
                    Command = "Api.csproj",
                    Arguments = "--urls http://localhost:5005",
                    CleanBeforeRestore = true,
                    EnvironmentVariables =
                    {
                        ["ASPNETCORE_ENVIRONMENT"] = "Development"
                    }
                }
            ]
        });

        var loaded = await store.LoadAsync();

        Assert.True(loaded.AlwaysOnTop);
        Assert.False(loaded.ShowNewestLogsFirst);
        Assert.False(loaded.ShowProjectPaths);
        Assert.Equal(0.45, loaded.RunnerOpacity);
        Assert.NotNull(loaded.WindowPlacement);
        Assert.Equal(120, loaded.WindowPlacement.X);
        Assert.Equal(80, loaded.WindowPlacement.Y);
        Assert.Equal(1440, loaded.WindowPlacement.Width);
        Assert.Equal(900, loaded.WindowPlacement.Height);
        Assert.True(loaded.WindowPlacement.IsMaximized);
        Assert.NotNull(loaded.SettingsWindowPlacement);
        Assert.Equal(240, loaded.SettingsWindowPlacement.X);
        Assert.Equal(160, loaded.SettingsWindowPlacement.Y);
        Assert.Equal(1000, loaded.SettingsWindowPlacement.Width);
        Assert.Equal(720, loaded.SettingsWindowPlacement.Height);
        Assert.False(loaded.SettingsWindowPlacement.IsMaximized);
        Assert.NotNull(loaded.DetailsWindowPlacement);
        Assert.Equal(300, loaded.DetailsWindowPlacement.X);
        Assert.Equal(220, loaded.DetailsWindowPlacement.Y);
        Assert.Equal(960, loaded.DetailsWindowPlacement.Width);
        Assert.Equal(640, loaded.DetailsWindowPlacement.Height);
        Assert.True(loaded.DetailsWindowPlacement.IsMaximized);
        var runner = Assert.Single(loaded.Runners);
        Assert.Equal("runner-1", runner.Id);
        Assert.Equal("API", runner.DisplayName);
        Assert.Equal(RunnerType.DotNetProject, runner.Type);
        Assert.Equal("Api.csproj", runner.Command);
        Assert.True(runner.CleanBeforeRestore);
        Assert.Equal("Development", runner.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"]);
    }

    [Fact]
    public async Task LoadAsync_WhenRunnerOmitsCleanBeforeRestore_DefaultsToFalse()
    {
        using var directory = TempDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        var store = new RunnerConfigStore(path);

        await File.WriteAllTextAsync(
            path,
            """
            {
              "runners": [
                {
                  "id": "runner-1",
                  "displayName": "API",
                  "type": "DotNetProject",
                  "workingDirectory": "C:\\src\\api",
                  "command": "",
                  "arguments": ""
                }
              ]
            }
            """);

        var config = await store.LoadAsync();

        var runner = Assert.Single(config.Runners);
        Assert.False(runner.CleanBeforeRestore);
    }

    [Fact]
    public async Task LoadAsync_WhenLogOrderPreferenceIsMissing_DefaultsToNewestFirst()
    {
        using var directory = TempDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        var store = new RunnerConfigStore(path);

        await File.WriteAllTextAsync(
            path,
            """
            {
              "alwaysOnTop": true,
              "runners": []
            }
            """);

        var config = await store.LoadAsync();

        Assert.True(config.ShowNewestLogsFirst);
    }

    [Fact]
    public async Task LoadAsync_WhenGeneralPreferencesAreMissing_DefaultsToPathsShownAndFullOpacity()
    {
        using var directory = TempDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        var store = new RunnerConfigStore(path);

        await File.WriteAllTextAsync(
            path,
            """
            {
              "alwaysOnTop": true,
              "runners": []
            }
            """);

        var config = await store.LoadAsync();

        Assert.True(config.ShowProjectPaths);
        Assert.Equal(1.0, config.RunnerOpacity);
    }

    [Fact]
    public async Task SaveAsyncAndLoadAsync_RoundTripsBuildOnlyProjectType()
    {
        using var directory = TempDirectory.Create();
        var path = Path.Combine(directory.Path, "settings.json");
        var store = new RunnerConfigStore(path);

        await store.SaveAsync(new RunnerConfig
        {
            Runners =
            [
                new RunnerDefinition
                {
                    DisplayName = "Build API",
                    Type = RunnerType.DotNetProjectBuild,
                    WorkingDirectory = "C:\\src\\api"
                }
            ]
        });

        var loaded = await store.LoadAsync();

        var runner = Assert.Single(loaded.Runners);
        Assert.Equal(RunnerType.DotNetProjectBuild, runner.Type);
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyConfig()
    {
        using var directory = TempDirectory.Create();
        var store = new RunnerConfigStore(Path.Combine(directory.Path, "missing.json"));

        var config = await store.LoadAsync();

        Assert.False(config.AlwaysOnTop);
        Assert.True(config.ShowNewestLogsFirst);
        Assert.True(config.ShowProjectPaths);
        Assert.Equal(1.0, config.RunnerOpacity);
        Assert.Null(config.WindowPlacement);
        Assert.Null(config.SettingsWindowPlacement);
        Assert.Null(config.DetailsWindowPlacement);
        Assert.Empty(config.Runners);
    }
}
