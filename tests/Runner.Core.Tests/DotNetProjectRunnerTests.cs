using Runner.Core.Runners;

namespace Runner.Core.Tests;

public sealed class DotNetProjectRunnerTests
{
    [Fact]
    public async Task StartAsyncAndStopAsync_ManageDotNetProjectProcess()
    {
        using var directory = TempDirectory.Create();
        CreateLongRunningProject(directory.Path);

        await using var runner = new DotNetProjectRunner(new RunnerDefinition
        {
            DisplayName = "Test app",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = directory.Path
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        try
        {
            await runner.StartAsync(timeout.Token);

            await WaitUntilAsync(
                () => runner.Status == RunnerStatus.Running && runner.ProcessId is not null,
                timeout.Token);

            Assert.Equal(RunnerStatus.Running, runner.Status);
            Assert.NotNull(runner.ProcessId);

            await runner.StopAsync(timeout.Token);

            Assert.Equal(RunnerStatus.Stopped, runner.Status);
            Assert.Null(runner.ProcessId);
            Assert.Null(runner.LastFailure);
        }
        finally
        {
            if (runner.Status is RunnerStatus.Starting or RunnerStatus.Running)
            {
                await runner.StopAsync(CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task StartAsync_WithInvalidDefinition_MarksRunnerFailed()
    {
        await using var runner = new DotNetProjectRunner(new RunnerDefinition
        {
            DisplayName = "Broken app",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync());
        Assert.Equal(RunnerStatus.Failed, runner.Status);
        Assert.NotNull(runner.LastFailure);
        Assert.Equal("Start failed.", runner.LastFailure.Reason);
        Assert.Contains("Working directory does not exist", runner.LastFailure.ExceptionMessage);
    }

    [Fact]
    public async Task ProcessExitWithNonZeroCode_MarksRunnerFailedAndCapturesDiagnostics()
    {
        using var directory = TempDirectory.Create();
        CreateFailingProject(directory.Path);

        await using var runner = new DotNetProjectRunner(new RunnerDefinition
        {
            DisplayName = "Failing app",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = directory.Path
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await runner.StartAsync(timeout.Token);
        await WaitUntilAsync(() => runner.Status == RunnerStatus.Failed, timeout.Token);

        var failure = Assert.IsType<RunnerFailureDetails>(runner.LastFailure);
        Assert.Equal(42, failure.ExitCode);
        Assert.Equal("Process exited with a non-zero exit code.", failure.Reason);
        Assert.Contains(failure.DiagnosticLines, line => line.Contains("stdout detail", StringComparison.Ordinal));
        Assert.Contains(failure.DiagnosticLines, line => line.Contains("stderr detail", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SuccessfulRun_ClearsStaleFailureDetails()
    {
        using var directory = TempDirectory.Create();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var definition = new RunnerDefinition
        {
            DisplayName = "Recovering app",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = missingPath
        };

        await using var runner = new DotNetProjectRunner(definition);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync());
        Assert.NotNull(runner.LastFailure);

        CreateSuccessfulProject(directory.Path);
        definition.WorkingDirectory = directory.Path;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await runner.StartAsync(timeout.Token);
        await WaitUntilAsync(() => runner.Status == RunnerStatus.Stopped, timeout.Token);

        Assert.Null(runner.LastFailure);
    }

    private static void CreateLongRunningProject(string directory)
    {
        File.WriteAllText(
            Path.Combine(directory, "Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(
            Path.Combine(directory, "Program.cs"),
            """
            Console.WriteLine("ready");
            await Task.Delay(TimeSpan.FromMinutes(5));
            """);
    }

    private static void CreateFailingProject(string directory)
    {
        CreateProjectFile(directory);

        File.WriteAllText(
            Path.Combine(directory, "Program.cs"),
            """
            Console.WriteLine("stdout detail");
            Console.Error.WriteLine("stderr detail");
            await Task.Delay(500);
            return 42;
            """);
    }

    private static void CreateSuccessfulProject(string directory)
    {
        CreateProjectFile(directory);

        File.WriteAllText(
            Path.Combine(directory, "Program.cs"),
            """
            Console.WriteLine("all good");
            await Task.Delay(100);
            return 0;
            """);
    }

    private static void CreateProjectFile(string directory)
    {
        File.WriteAllText(
            Path.Combine(directory, "Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(100, cancellationToken);
        }
    }
}
