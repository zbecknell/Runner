using Runner.App.ViewModels;
using Runner.App.Services;
using Runner.Core.Config;
using Runner.Core.Runners;
using Optris.Icons.Avalonia;

namespace Runner.Core.Tests;

public sealed class RunnerViewModelTests
{
    [Theory]
    [InlineData(RunnerStatus.Running, "#166534", "#ECFDF5", "#22C55E", "fa-solid fa-circle-play", IconAnimation.None)]
    [InlineData(RunnerStatus.Failed, "#991B1B", "#FEF2F2", "#EF4444", "fa-solid fa-triangle-exclamation", IconAnimation.None)]
    [InlineData(RunnerStatus.Cleaning, "#1D4ED8", "#EFF6FF", "#60A5FA", "fa-solid fa-spinner", IconAnimation.Spin)]
    [InlineData(RunnerStatus.Restoring, "#1D4ED8", "#EFF6FF", "#60A5FA", "fa-solid fa-spinner", IconAnimation.Spin)]
    [InlineData(RunnerStatus.Building, "#1D4ED8", "#EFF6FF", "#60A5FA", "fa-solid fa-spinner", IconAnimation.Spin)]
    [InlineData(RunnerStatus.Starting, "#1D4ED8", "#EFF6FF", "#60A5FA", "fa-solid fa-spinner", IconAnimation.Spin)]
    [InlineData(RunnerStatus.Stopping, "#92400E", "#FFFBEB", "#F59E0B", "fa-solid fa-circle-stop", IconAnimation.None)]
    [InlineData(RunnerStatus.Stopped, "#334155", "#F8FAFC", "#64748B", "fa-solid fa-circle", IconAnimation.None)]
    public void StatusColorProperties_MapStatusToExpectedColors(
        RunnerStatus status,
        string expectedBackground,
        string expectedForeground,
        string expectedBorder,
        string expectedIcon,
        IconAnimation expectedIconAnimation)
    {
        var viewModel = CreateViewModel(new FakeRunner(status));

        Assert.Equal(expectedBackground, viewModel.StatusBackgroundColor);
        Assert.Equal(expectedForeground, viewModel.StatusForegroundColor);
        Assert.Equal(expectedBorder, viewModel.StatusBorderColor);
        Assert.Equal(expectedIcon, viewModel.StatusIconValue);
        Assert.Equal(expectedIconAnimation, viewModel.StatusIconAnimation);
    }

    [Theory]
    [InlineData(RunnerStatus.Running, "Stop", "fa-solid fa-stop", IconAnimation.None, "Stop", true)]
    [InlineData(RunnerStatus.Failed, "Run", "fa-solid fa-play", IconAnimation.None, "Run", true)]
    [InlineData(RunnerStatus.Stopped, "Run", "fa-solid fa-play", IconAnimation.None, "Run", true)]
    [InlineData(RunnerStatus.Cleaning, "Stop", "fa-solid fa-stop", IconAnimation.None, "Stop", true)]
    [InlineData(RunnerStatus.Restoring, "Stop", "fa-solid fa-stop", IconAnimation.None, "Stop", true)]
    [InlineData(RunnerStatus.Building, "Stop", "fa-solid fa-stop", IconAnimation.None, "Stop", true)]
    [InlineData(RunnerStatus.Starting, "Stop", "fa-solid fa-stop", IconAnimation.None, "Stop", true)]
    [InlineData(RunnerStatus.Stopping, "Stopping", "fa-solid fa-spinner", IconAnimation.Spin, "Stopping", false)]
    public void PrimaryRunProperties_MapStatusToExpectedAction(
        RunnerStatus status,
        string expectedText,
        string expectedIcon,
        IconAnimation expectedIconAnimation,
        string expectedToolTip,
        bool expectedCanRun)
    {
        var viewModel = CreateViewModel(new FakeRunner(status));

        Assert.Equal(expectedText, viewModel.PrimaryRunText);
        Assert.Equal(expectedIcon, viewModel.PrimaryRunIconValue);
        Assert.Equal(expectedIconAnimation, viewModel.PrimaryRunIconAnimation);
        Assert.Equal(expectedToolTip, viewModel.PrimaryRunToolTip);
        Assert.Equal(expectedCanRun, viewModel.CanRunPrimary);
    }

    [Theory]
    [InlineData(RunnerStatus.Stopped)]
    [InlineData(RunnerStatus.Failed)]
    public void PrimaryRunProperties_ForBuildOnlyProject_ShowBuildAction(RunnerStatus status)
    {
        var viewModel = CreateViewModel(new FakeRunner(status, type: RunnerType.DotNetProjectBuild));

        Assert.Equal("Build", viewModel.PrimaryRunText);
        Assert.Equal("fa-solid fa-hammer", viewModel.PrimaryRunIconValue);
        Assert.Equal("Build", viewModel.PrimaryRunToolTip);
        Assert.True(viewModel.CanRunPrimary);
    }

    [Fact]
    public void AvailableTypes_IncludeCustomCommands()
    {
        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Stopped));

        Assert.Contains(
            viewModel.AvailableTypes,
            option => option.Type == RunnerType.CustomCommands && option.DisplayName == "Custom commands");
    }

    [Fact]
    public void CustomCommandsProject_HidesProcessTextAndRestartAction()
    {
        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Running, type: RunnerType.CustomCommands));

        Assert.True(viewModel.IsCustomCommands);
        Assert.False(viewModel.IsProcessTextVisible);
        Assert.Equal("", viewModel.ProcessText);
        Assert.False(viewModel.CanRestart);
        Assert.False(viewModel.CanShowRestartAction);
        Assert.Equal("Stop", viewModel.PrimaryRunText);
        Assert.True(viewModel.CanStop);
    }

    [Fact]
    public void CustomCommandProperties_UpdateDefinition()
    {
        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Stopped, type: RunnerType.CustomCommands));

        viewModel.CustomCleanCommand = "npm run clean";
        viewModel.CustomRestoreCommand = "npm install";
        viewModel.CustomBuildCommand = "npm run build";
        viewModel.CustomRunCommand = "npm start";

        Assert.Equal("npm run clean", viewModel.Definition.CustomCommands.Clean);
        Assert.Equal("npm install", viewModel.Definition.CustomCommands.Restore);
        Assert.Equal("npm run build", viewModel.Definition.CustomCommands.Build);
        Assert.Equal("npm start", viewModel.Definition.CustomCommands.Run);
    }

    [Theory]
    [InlineData(RunnerStatus.Cleaning, true)]
    [InlineData(RunnerStatus.Restoring, true)]
    [InlineData(RunnerStatus.Building, true)]
    [InlineData(RunnerStatus.Starting, true)]
    [InlineData(RunnerStatus.Running, true)]
    [InlineData(RunnerStatus.Stopping, false)]
    [InlineData(RunnerStatus.Stopped, false)]
    [InlineData(RunnerStatus.Failed, false)]
    public void CanStop_IsEnabledOnlyForActiveStartAndRunPhases(
        RunnerStatus status,
        bool expectedCanStop)
    {
        var viewModel = CreateViewModel(new FakeRunner(status));

        Assert.Equal(expectedCanStop, viewModel.CanStop);
    }

    [Theory]
    [InlineData(RunnerStatus.Stopped, RunnerType.DotNetProject, true, false, true, true)]
    [InlineData(RunnerStatus.Failed, RunnerType.DotNetProject, true, false, true, true)]
    [InlineData(RunnerStatus.Running, RunnerType.DotNetProject, false, true, false, false)]
    [InlineData(RunnerStatus.Stopped, RunnerType.DotNetProjectBuild, false, false, true, true)]
    [InlineData(RunnerStatus.Running, RunnerType.DotNetProjectBuild, false, false, false, false)]
    [InlineData(RunnerStatus.Stopped, RunnerType.CustomCommands, true, false, true, true)]
    [InlineData(RunnerStatus.Running, RunnerType.CustomCommands, false, false, false, false)]
    [InlineData(RunnerStatus.Stopping, RunnerType.DotNetProject, false, false, false, false)]
    public void ContextActionProperties_MapTypeAndStatusToExpectedAvailability(
        RunnerStatus status,
        RunnerType type,
        bool expectedCanStart,
        bool expectedCanRestart,
        bool expectedCanClean,
        bool expectedCanBuild)
    {
        var viewModel = CreateViewModel(new FakeRunner(status, type: type));

        Assert.Equal(expectedCanStart, viewModel.CanStart);
        Assert.Equal(expectedCanRestart, viewModel.CanRestart);
        Assert.Equal(expectedCanClean, viewModel.CanClean);
        Assert.Equal(expectedCanBuild, viewModel.CanBuild);
    }

    [Fact]
    public void DashboardStatus_ForIdleBuildOnlyProject_HidesStoppedPill()
    {
        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Stopped, type: RunnerType.DotNetProjectBuild));

        Assert.False(viewModel.IsDashboardStatusVisible);
        Assert.Equal("Stopped", viewModel.DashboardStatusText);
        Assert.Null(viewModel.DashboardStatusToolTip);
    }

    [Theory]
    [InlineData(RunnerStatus.Building, "Building")]
    [InlineData(RunnerStatus.Cleaning, "Cleaning")]
    [InlineData(RunnerStatus.Failed, "Failed")]
    public void DashboardStatus_ForActiveOrFailedBuildOnlyProject_ShowsStatusPill(
        RunnerStatus status,
        string expectedText)
    {
        var viewModel = CreateViewModel(new FakeRunner(status, type: RunnerType.DotNetProjectBuild));

        Assert.True(viewModel.IsDashboardStatusVisible);
        Assert.Equal(expectedText, viewModel.DashboardStatusText);
        Assert.Null(viewModel.DashboardStatusToolTip);
    }

    [Fact]
    public async Task DashboardStatus_ForSuccessfulBuildOnlyBuild_ShowsFinishedPill()
    {
        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Stopped, type: RunnerType.DotNetProjectBuild));

        await viewModel.BuildAsync();

        Assert.NotNull(viewModel.LastFinishedAt);
        Assert.True(viewModel.IsDashboardStatusVisible);
        Assert.Equal("Finished", viewModel.DashboardStatusText);
        Assert.StartsWith("Finished at ", viewModel.DashboardStatusToolTip);
        Assert.Equal("fa-solid fa-circle-check", viewModel.DashboardStatusIconValue);
    }

    [Fact]
    public async Task DashboardStatus_ForSuccessfulBuildOnlyClean_ShowsFinishedPill()
    {
        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Stopped, type: RunnerType.DotNetProjectBuild));

        await viewModel.CleanAsync();

        Assert.NotNull(viewModel.LastFinishedAt);
        Assert.True(viewModel.IsDashboardStatusVisible);
        Assert.Equal("Finished", viewModel.DashboardStatusText);
    }

    [Fact]
    public async Task DashboardStatus_WhenTypeChanges_ClearsPreviousFinishedPill()
    {
        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Stopped, type: RunnerType.DotNetProjectBuild));
        await viewModel.BuildAsync();

        viewModel.Type = RunnerType.DotNetProject;

        Assert.Null(viewModel.LastFinishedAt);
    }

    [Fact]
    public void FailureDetailsProperties_FollowRunnerFailureDetails()
    {
        var failure = new RunnerFailureDetails(
            DateTimeOffset.Parse("2026-05-08T12:00:00-04:00"),
            "Process exited with a non-zero exit code.",
            42,
            "boom",
            ["[12:00:00] [err] stderr detail"]);

        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Failed, failure));

        Assert.True(viewModel.HasFailureDetails);
        Assert.True(viewModel.ShowFailureDetailsCommand.CanExecute(null));
        Assert.Contains("Exit code: 42", viewModel.FailureDetailsText);
        Assert.Contains("stderr detail", viewModel.FailureDetailsText);
    }

    [Fact]
    public void FailureDetailsCommands_ToggleInlineDetailsPanel()
    {
        var failure = new RunnerFailureDetails(
            DateTimeOffset.Now,
            "Start failed.",
            null,
            "missing folder",
            []);

        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Failed, failure));

        Assert.False(viewModel.IsFailureDetailsVisible);

        viewModel.ShowFailureDetailsCommand.Execute(null);
        Assert.True(viewModel.IsFailureDetailsVisible);

        viewModel.HideFailureDetailsCommand.Execute(null);
        Assert.False(viewModel.IsFailureDetailsVisible);
    }

    [Fact]
    public void FailureDetailsCommand_DisabledWhenNoFailureExists()
    {
        var viewModel = CreateViewModel(new FakeRunner(RunnerStatus.Running));

        Assert.False(viewModel.HasFailureDetails);
        Assert.False(viewModel.ShowFailureDetailsCommand.CanExecute(null));
        Assert.Equal("", viewModel.FailureDetailsText);
    }

    [Fact]
    public void LogProperties_FollowRunnerLogLines()
    {
        var viewModel = CreateViewModel(new FakeRunner(
            RunnerStatus.Running,
            logLines: ["[12:00:00] [out] ready", "[12:00:01] [err] warning"]));

        Assert.True(viewModel.HasLogs);
        Assert.Contains("ready", viewModel.LogText);
        Assert.Contains("warning", viewModel.LogText);
    }

    [Fact]
    public void ClearLogsCommand_ClearsBackingRunnerLogs()
    {
        var viewModel = CreateViewModel(new FakeRunner(
            RunnerStatus.Running,
            logLines: ["[12:00:00] [out] ready"]));

        viewModel.ClearLogsCommand.Execute(null);

        Assert.False(viewModel.HasLogs);
        Assert.Equal("", viewModel.LogText);
    }

    [Fact]
    public void DetailsLogText_DefaultsToNewestFirst()
    {
        using var directory = TempDirectory.Create();
        var dashboard = CreateDashboard(directory.Path);
        var runner = CreateViewModel(new FakeRunner(
            RunnerStatus.Running,
            logLines:
            [
                "[12:00:00] [out] first",
                "[12:00:01] [out] second"
            ]));
        using var details = new RunnerDetailsViewModel(dashboard, runner);

        var firstRenderedLine = details.LogText.Split(Environment.NewLine)[0];

        Assert.Contains("second", firstRenderedLine);
        Assert.Equal(
            ["[12:00:00] [out] first", "[12:00:01] [out] second"],
            runner.LogLines);
    }

    [Fact]
    public void DetailsLogText_CanRenderOldestFirst()
    {
        using var directory = TempDirectory.Create();
        var dashboard = CreateDashboard(directory.Path);
        dashboard.ShowNewestLogsFirst = false;
        var runner = CreateViewModel(new FakeRunner(
            RunnerStatus.Running,
            logLines:
            [
                "[12:00:00] [out] first",
                "[12:00:01] [out] second"
            ]));
        using var details = new RunnerDetailsViewModel(dashboard, runner);

        var firstRenderedLine = details.LogText.Split(Environment.NewLine)[0];

        Assert.Contains("first", firstRenderedLine);
        Assert.Equal(
            ["[12:00:00] [out] first", "[12:00:01] [out] second"],
            runner.LogLines);
    }

    [Fact]
    public async Task TypeChange_RecreatesBackingRunner()
    {
        var definition = new RunnerDefinition
        {
            DisplayName = "Switchable runner",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = Path.GetTempPath()
        };
        var factory = new RecordingRunnerFactory();
        var viewModel = new RunnerViewModel(definition, factory);

        viewModel.Type = RunnerType.DotNetProjectBuild;
        await viewModel.StartAsync();

        Assert.Equal(RunnerType.DotNetProjectBuild, viewModel.Type);
        Assert.Equal(2, factory.Created.Count);
        Assert.Equal(1, factory.Created[0].DisposeCount);
        Assert.Equal(0, factory.Created[0].StartCount);
        Assert.Equal(1, factory.Created[1].StartCount);
    }

    private static RunnerViewModel CreateViewModel(FakeRunner runner)
    {
        return new RunnerViewModel(runner.Definition, new FakeRunnerFactory(runner));
    }

    private static MainWindowViewModel CreateDashboard(string directory)
    {
        return new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory, "settings.json")),
            new RunnerFactory(),
            NullWorkingDirectoryPicker.Instance);
    }

    private sealed class FakeRunnerFactory : IRunnerFactory
    {
        private readonly FakeRunner _runner;

        public FakeRunnerFactory(FakeRunner runner)
        {
            _runner = runner;
        }

        public IRunner Create(RunnerDefinition definition)
        {
            return _runner;
        }
    }

    private sealed class FakeRunner : IRunner
    {
        public FakeRunner(
            RunnerStatus status,
            RunnerFailureDetails? lastFailure = null,
            RunnerType type = RunnerType.DotNetProject,
            IReadOnlyList<string>? logLines = null)
        {
            Status = status;
            LastFailure = lastFailure;
            Definition.Type = type;
            _logLines = logLines?.ToList() ?? [];
        }

        private readonly List<string> _logLines;

        public event EventHandler<RunnerStatus>? StatusChanged;

        public RunnerDefinition Definition { get; } = new()
        {
            DisplayName = "Fake runner",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = Path.GetTempPath()
        };

        public RunnerStatus Status { get; private set; }

        public int? ProcessId => Status == RunnerStatus.Running && Definition.Type != RunnerType.CustomCommands
            ? 123
            : null;

        public RunnerFailureDetails? LastFailure { get; private set; }

        public IReadOnlyList<string> LogLines => _logLines.ToArray();

        public void ClearLogs()
        {
            _logLines.Clear();
        }

        public Task CleanAsync(CancellationToken cancellationToken = default)
        {
            Status = RunnerStatus.Stopped;
            LastFailure = null;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public Task BuildAsync(CancellationToken cancellationToken = default)
        {
            Status = RunnerStatus.Stopped;
            LastFailure = null;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            Status = RunnerStatus.Running;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Status = RunnerStatus.Stopped;
            LastFailure = null;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public async Task RestartAsync(CancellationToken cancellationToken = default)
        {
            await StopAsync(cancellationToken);
            await StartAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingRunnerFactory : IRunnerFactory
    {
        public List<RecordingRunner> Created { get; } = [];

        public IRunner Create(RunnerDefinition definition)
        {
            var runner = new RecordingRunner(definition.Clone());
            Created.Add(runner);
            return runner;
        }
    }

    private sealed class RecordingRunner : IRunner
    {
        public RecordingRunner(RunnerDefinition definition)
        {
            Definition = definition;
        }

        public event EventHandler<RunnerStatus>? StatusChanged;

        public RunnerDefinition Definition { get; }

        public RunnerStatus Status { get; private set; }

        public int? ProcessId => null;

        public RunnerFailureDetails? LastFailure => null;

        public IReadOnlyList<string> LogLines => [];

        public void ClearLogs()
        {
        }

        public Task CleanAsync(CancellationToken cancellationToken = default)
        {
            Status = RunnerStatus.Stopped;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public Task BuildAsync(CancellationToken cancellationToken = default)
        {
            Status = RunnerStatus.Stopped;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public int StartCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Status = RunnerStatus.Stopped;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public async Task RestartAsync(CancellationToken cancellationToken = default)
        {
            await StopAsync(cancellationToken);
            await StartAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
