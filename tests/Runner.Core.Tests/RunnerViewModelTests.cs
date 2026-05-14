using Runner.App.ViewModels;
using Runner.Core.Runners;
using Optris.Icons.Avalonia;

namespace Runner.Core.Tests;

public sealed class RunnerViewModelTests
{
    [Theory]
    [InlineData(RunnerStatus.Running, "#166534", "#ECFDF5", "#22C55E", "fa-solid fa-circle-play", IconAnimation.None)]
    [InlineData(RunnerStatus.Failed, "#991B1B", "#FEF2F2", "#EF4444", "fa-solid fa-triangle-exclamation", IconAnimation.None)]
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
    [InlineData(RunnerStatus.Running, "Restart", "fa-solid fa-rotate-right", IconAnimation.None, "Restart", true)]
    [InlineData(RunnerStatus.Failed, "Start", "fa-solid fa-play", IconAnimation.None, "Start", true)]
    [InlineData(RunnerStatus.Stopped, "Start", "fa-solid fa-play", IconAnimation.None, "Start", true)]
    [InlineData(RunnerStatus.Restoring, "Restoring", "fa-solid fa-spinner", IconAnimation.Spin, "Restoring", false)]
    [InlineData(RunnerStatus.Building, "Building", "fa-solid fa-spinner", IconAnimation.Spin, "Building", false)]
    [InlineData(RunnerStatus.Starting, "Starting", "fa-solid fa-spinner", IconAnimation.Spin, "Starting", false)]
    [InlineData(RunnerStatus.Stopping, "Start", "fa-solid fa-play", IconAnimation.None, "Start", false)]
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

    private static RunnerViewModel CreateViewModel(FakeRunner runner)
    {
        return new RunnerViewModel(runner.Definition, new FakeRunnerFactory(runner));
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
        public FakeRunner(RunnerStatus status, RunnerFailureDetails? lastFailure = null)
        {
            Status = status;
            LastFailure = lastFailure;
        }

        public event EventHandler<RunnerStatus>? StatusChanged;

        public RunnerDefinition Definition { get; } = new()
        {
            DisplayName = "Fake runner",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = Path.GetTempPath()
        };

        public RunnerStatus Status { get; private set; }

        public int? ProcessId => Status == RunnerStatus.Running ? 123 : null;

        public RunnerFailureDetails? LastFailure { get; private set; }

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
}
