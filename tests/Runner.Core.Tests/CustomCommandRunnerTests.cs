using Runner.Core.Runners;

namespace Runner.Core.Tests;

public sealed class CustomCommandRunnerTests
{
    [Fact]
    public async Task StartAsync_RunsConfiguredCommandsInOrderAndSkipsBlankCommands()
    {
        using var directory = TempDirectory.Create();
        var outputPath = Path.Combine(directory.Path, "phases.txt");
        await using var runner = new CustomCommandRunner(new RunnerDefinition
        {
            DisplayName = "Custom app",
            Type = RunnerType.CustomCommands,
            WorkingDirectory = directory.Path,
            CleanBeforeRestore = true,
            CustomCommands = new RunnerCommandSet
            {
                Clean = AppendLineCommand("clean", "phases.txt"),
                Restore = "",
                Build = AppendLineCommand("build", "phases.txt"),
                Run = AppendLineCommand("run", "phases.txt")
            }
        });
        var observedStatuses = new List<RunnerStatus>();
        runner.StatusChanged += (_, status) => observedStatuses.Add(status);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await runner.StartAsync(timeout.Token);

        Assert.Equal(RunnerStatus.Stopped, runner.Status);
        Assert.Null(runner.ProcessId);
        Assert.Null(runner.LastFailure);
        Assert.Equal(
            ["clean", "build", "run"],
            await ReadLinesAsync(outputPath, timeout.Token));
        Assert.Contains(RunnerStatus.Cleaning, observedStatuses);
        Assert.DoesNotContain(RunnerStatus.Restoring, observedStatuses);
        Assert.Contains(RunnerStatus.Building, observedStatuses);
        Assert.Contains(RunnerStatus.Running, observedStatuses);
    }

    [Fact]
    public async Task StartAsync_WhenCleanBeforeRestoreIsFalse_SkipsCleanCommand()
    {
        using var directory = TempDirectory.Create();
        var outputPath = Path.Combine(directory.Path, "phases.txt");
        await using var runner = new CustomCommandRunner(new RunnerDefinition
        {
            DisplayName = "Custom app",
            Type = RunnerType.CustomCommands,
            WorkingDirectory = directory.Path,
            CleanBeforeRestore = false,
            CustomCommands = new RunnerCommandSet
            {
                Clean = AppendLineCommand("clean", "phases.txt"),
                Restore = AppendLineCommand("restore", "phases.txt"),
                Build = AppendLineCommand("build", "phases.txt"),
                Run = AppendLineCommand("run", "phases.txt")
            }
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await runner.StartAsync(timeout.Token);

        Assert.Equal(
            ["restore", "build", "run"],
            await ReadLinesAsync(outputPath, timeout.Token));
    }

    [Fact]
    public async Task BuildAsync_RunsRestoreThenBuildOnly()
    {
        using var directory = TempDirectory.Create();
        var outputPath = Path.Combine(directory.Path, "phases.txt");
        await using var runner = new CustomCommandRunner(new RunnerDefinition
        {
            DisplayName = "Custom build",
            Type = RunnerType.CustomCommands,
            WorkingDirectory = directory.Path,
            CustomCommands = new RunnerCommandSet
            {
                Clean = AppendLineCommand("clean", "phases.txt"),
                Restore = AppendLineCommand("restore", "phases.txt"),
                Build = AppendLineCommand("build", "phases.txt"),
                Run = AppendLineCommand("run", "phases.txt")
            }
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await runner.BuildAsync(timeout.Token);

        Assert.Equal(
            ["restore", "build"],
            await ReadLinesAsync(outputPath, timeout.Token));
    }

    [Fact]
    public async Task CleanAsync_WhenCommandIsBlank_CompletesWithoutLogs()
    {
        using var directory = TempDirectory.Create();
        await using var runner = new CustomCommandRunner(new RunnerDefinition
        {
            DisplayName = "Blank clean",
            Type = RunnerType.CustomCommands,
            WorkingDirectory = directory.Path
        });

        await runner.CleanAsync();

        Assert.Equal(RunnerStatus.Stopped, runner.Status);
        Assert.Empty(runner.LogLines);
        Assert.Null(runner.LastFailure);
    }

    [Fact]
    public async Task StartAsync_AppliesWorkingDirectoryAndEnvironmentVariables()
    {
        using var directory = TempDirectory.Create();
        var envPath = Path.Combine(directory.Path, "env.txt");
        var cwdPath = Path.Combine(directory.Path, "cwd.txt");
        await using var runner = new CustomCommandRunner(new RunnerDefinition
        {
            DisplayName = "Custom env",
            Type = RunnerType.CustomCommands,
            WorkingDirectory = directory.Path,
            CustomCommands = new RunnerCommandSet
            {
                Build = WriteEnvironmentVariableCommand("RUNNER_CUSTOM_FLAG", "env.txt"),
                Run = WriteWorkingDirectoryCommand("cwd.txt")
            },
            EnvironmentVariables =
            {
                ["RUNNER_CUSTOM_FLAG"] = "from-env"
            }
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await runner.StartAsync(timeout.Token);

        Assert.Equal("from-env", (await File.ReadAllTextAsync(envPath, timeout.Token)).Trim());
        Assert.Equal(directory.Path, (await File.ReadAllTextAsync(cwdPath, timeout.Token)).Trim());
    }

    [Fact]
    public async Task StartAsync_WhenRunCommandFails_MarksRunnerFailedAndCapturesDiagnostics()
    {
        using var directory = TempDirectory.Create();
        await using var runner = new CustomCommandRunner(new RunnerDefinition
        {
            DisplayName = "Custom failure",
            Type = RunnerType.CustomCommands,
            WorkingDirectory = directory.Path,
            CustomCommands = new RunnerCommandSet
            {
                Run = FailingCommand()
            }
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync());

        var failure = Assert.IsType<RunnerFailureDetails>(runner.LastFailure);
        Assert.Equal(RunnerStatus.Failed, runner.Status);
        Assert.Equal("Run failed.", failure.Reason);
        Assert.Equal(42, failure.ExitCode);
        Assert.Contains(failure.DiagnosticLines, line => line.Contains("stdout detail", StringComparison.Ordinal));
        Assert.Contains(failure.DiagnosticLines, line => line.Contains("stderr detail", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_ReportsNoProcessIdWhileRunCommandIsActive()
    {
        using var directory = TempDirectory.Create();
        await using var runner = new CustomCommandRunner(new RunnerDefinition
        {
            DisplayName = "Custom active",
            Type = RunnerType.CustomCommands,
            WorkingDirectory = directory.Path,
            CustomCommands = new RunnerCommandSet
            {
                Run = LongRunningCommand()
            }
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var startTask = runner.StartAsync(timeout.Token);

        await WaitUntilAsync(() => runner.Status == RunnerStatus.Running, timeout.Token);

        Assert.Null(runner.ProcessId);

        await runner.StopAsync(timeout.Token);
        await startTask;
    }

    [Fact]
    public async Task StopAsync_CancelsActiveCommandAndReturnsToStopped()
    {
        using var directory = TempDirectory.Create();
        await using var runner = new CustomCommandRunner(new RunnerDefinition
        {
            DisplayName = "Custom cancel",
            Type = RunnerType.CustomCommands,
            WorkingDirectory = directory.Path,
            CustomCommands = new RunnerCommandSet
            {
                Run = LongRunningCommand()
            }
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var startTask = runner.StartAsync(timeout.Token);

        await WaitUntilAsync(() => runner.Status == RunnerStatus.Running, timeout.Token);
        await runner.StopAsync(timeout.Token);
        await startTask;

        Assert.Equal(RunnerStatus.Stopped, runner.Status);
        Assert.Null(runner.ProcessId);
        Assert.Null(runner.LastFailure);
    }

    private static async Task<string[]> ReadLinesAsync(string path, CancellationToken cancellationToken)
    {
        return (await File.ReadAllLinesAsync(path, cancellationToken))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static string AppendLineCommand(string text, string fileName)
    {
        return OperatingSystem.IsWindows()
            ? $"echo {text}>>{fileName}"
            : $"echo {text} >> {fileName}";
    }

    private static string WriteEnvironmentVariableCommand(string variableName, string fileName)
    {
        return OperatingSystem.IsWindows()
            ? $"echo %{variableName}%>{fileName}"
            : $"printf '%s\\n' \"${variableName}\" > {fileName}";
    }

    private static string WriteWorkingDirectoryCommand(string fileName)
    {
        return OperatingSystem.IsWindows()
            ? $"echo %CD%>{fileName}"
            : $"pwd > {fileName}";
    }

    private static string FailingCommand()
    {
        return OperatingSystem.IsWindows()
            ? "echo stdout detail & echo stderr detail 1>&2 & exit /b 42"
            : "echo stdout detail; echo stderr detail >&2; exit 42";
    }

    private static string LongRunningCommand()
    {
        return OperatingSystem.IsWindows()
            ? "ping 127.0.0.1 -n 30 > nul"
            : "sleep 30";
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
