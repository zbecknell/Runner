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
                    EnvironmentVariables =
                    {
                        ["ASPNETCORE_ENVIRONMENT"] = "Development"
                    }
                }
            ]
        });

        var loaded = await store.LoadAsync();

        Assert.True(loaded.AlwaysOnTop);
        var runner = Assert.Single(loaded.Runners);
        Assert.Equal("runner-1", runner.Id);
        Assert.Equal("API", runner.DisplayName);
        Assert.Equal(RunnerType.DotNetProject, runner.Type);
        Assert.Equal("Api.csproj", runner.Command);
        Assert.Equal("Development", runner.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"]);
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyConfig()
    {
        using var directory = TempDirectory.Create();
        var store = new RunnerConfigStore(Path.Combine(directory.Path, "missing.json"));

        var config = await store.LoadAsync();

        Assert.False(config.AlwaysOnTop);
        Assert.Empty(config.Runners);
    }
}
