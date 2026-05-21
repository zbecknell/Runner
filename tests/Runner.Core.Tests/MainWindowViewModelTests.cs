using Runner.App.Services;
using Runner.App.ViewModels;
using Runner.Core.Config;
using Runner.Core.Runners;

namespace Runner.Core.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_DefaultsToDashboardMode()
    {
        using var directory = TempDirectory.Create();
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));

        Assert.False(viewModel.IsEditMode);
        Assert.True(viewModel.IsDashboardMode);
        Assert.Equal("Edit", viewModel.EditModeButtonText);
        Assert.Equal("fa-solid fa-pen", viewModel.EditModeIconValue);
    }

    [Fact]
    public void ToggleEditModeCommand_SwitchesBetweenDashboardAndEditMode()
    {
        using var directory = TempDirectory.Create();
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));

        viewModel.ToggleEditModeCommand.Execute(null);

        Assert.True(viewModel.IsEditMode);
        Assert.False(viewModel.IsDashboardMode);
        Assert.Equal("Done", viewModel.EditModeButtonText);
        Assert.Equal("fa-solid fa-check", viewModel.EditModeIconValue);

        viewModel.ToggleEditModeCommand.Execute(null);

        Assert.False(viewModel.IsEditMode);
        Assert.True(viewModel.IsDashboardMode);
    }

    [Fact]
    public async Task AddRunnerCommand_FromEmptyDashboard_CreatesRunnerSelectsItAndEntersEditMode()
    {
        using var directory = TempDirectory.Create();
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.AddRunnerCommand.ExecuteAsync(null);

        var runner = Assert.Single(viewModel.Runners);
        Assert.Same(runner, viewModel.SelectedRunner);
        Assert.True(viewModel.IsEditMode);
    }

    [Fact]
    public async Task CloneSelectedCommand_CopiesSelectedRunnerConfigurationSelectsCloneAndSavesConfig()
    {
        using var directory = TempDirectory.Create();
        var sourceDefinition = new RunnerDefinition
        {
            Id = "source-runner",
            DisplayName = "API",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = directory.Path,
            Command = "Api.csproj",
            Arguments = "--urls http://localhost:5005",
            EnvironmentVariables =
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            }
        };
        var configStore = CreateConfigStore(
            directory.Path,
            sourceDefinition,
            CreateRunnerDefinition("Worker", directory.Path));
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.CloneSelectedCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Runners.Count);
        Assert.Equal("API", viewModel.Runners[0].DisplayName);
        Assert.Equal("Worker", viewModel.Runners[2].DisplayName);

        var clone = viewModel.Runners[1];
        Assert.Same(clone, viewModel.SelectedRunner);
        Assert.True(viewModel.IsEditMode);
        Assert.Equal("Cloned runner.", viewModel.StatusMessage);
        Assert.Equal("API (Clone)", clone.DisplayName);
        Assert.Equal(RunnerType.DotNetProject, clone.Type);
        Assert.Equal(directory.Path, clone.WorkingDirectory);
        Assert.Equal("Api.csproj", clone.Command);
        Assert.Equal("--urls http://localhost:5005", clone.Arguments);
        Assert.Equal("Development", clone.Definition.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"]);
        Assert.False(string.IsNullOrWhiteSpace(clone.Id));
        Assert.NotEqual("source-runner", clone.Id);

        clone.Definition.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
        Assert.Equal(
            "Development",
            viewModel.Runners[0].Definition.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"]);

        var savedConfig = await configStore.LoadAsync();
        Assert.Equal(3, savedConfig.Runners.Count);
        Assert.Equal("API (Clone)", savedConfig.Runners[1].DisplayName);
        Assert.Equal("source-runner", savedConfig.Runners[0].Id);
        Assert.NotEqual("source-runner", savedConfig.Runners[1].Id);
        Assert.Equal(
            "Development",
            savedConfig.Runners[1].EnvironmentVariables["ASPNETCORE_ENVIRONMENT"]);
    }

    [Fact]
    public void CloneSelectedCommand_WhenNoRunnerSelected_IsNotExecutable()
    {
        using var directory = TempDirectory.Create();
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(directory.Path),
            new FakeRunnerRemovalConfirmation(true));

        Assert.False(viewModel.CloneSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task ToggleAlwaysOnTopCommand_TogglesAndSavesPreference()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.ToggleAlwaysOnTopCommand.ExecuteAsync(null);

        Assert.True(viewModel.AlwaysOnTop);

        var savedConfig = await configStore.LoadAsync();
        Assert.True(savedConfig.AlwaysOnTop);
    }

    [Fact]
    public async Task LoadAsync_LoadsWindowPlacement()
    {
        using var directory = TempDirectory.Create();
        var placement = new WindowPlacement
        {
            X = 320,
            Y = 180,
            Width = 1280,
            Height = 760,
            IsMaximized = true
        };
        var configStore = new RunnerConfigStore(Path.Combine(directory.Path, "settings.json"));
        await configStore.SaveAsync(new RunnerConfig
        {
            WindowPlacement = placement,
            Runners = [CreateRunnerDefinition("Test app", directory.Path)]
        });
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));

        await viewModel.LoadAsync();

        Assert.NotNull(viewModel.WindowPlacement);
        Assert.Equal(320, viewModel.WindowPlacement.X);
        Assert.Equal(180, viewModel.WindowPlacement.Y);
        Assert.Equal(1280, viewModel.WindowPlacement.Width);
        Assert.Equal(760, viewModel.WindowPlacement.Height);
        Assert.True(viewModel.WindowPlacement.IsMaximized);
    }

    [Fact]
    public async Task SaveWindowPlacementAsync_PersistsPlacementWithoutDroppingConfig()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();
        await viewModel.ToggleAlwaysOnTopCommand.ExecuteAsync(null);

        await viewModel.SaveWindowPlacementAsync(new WindowPlacement
        {
            X = 48,
            Y = 64,
            Width = 1180,
            Height = 840,
            IsMaximized = false
        });

        var savedConfig = await configStore.LoadAsync();
        Assert.True(savedConfig.AlwaysOnTop);
        Assert.Single(savedConfig.Runners);
        Assert.NotNull(savedConfig.WindowPlacement);
        Assert.Equal(48, savedConfig.WindowPlacement.X);
        Assert.Equal(64, savedConfig.WindowPlacement.Y);
        Assert.Equal(1180, savedConfig.WindowPlacement.Width);
        Assert.Equal(840, savedConfig.WindowPlacement.Height);
        Assert.False(savedConfig.WindowPlacement.IsMaximized);
    }

    [Fact]
    public async Task SaveConfigAsync_PreservesLoadedWindowPlacement()
    {
        using var directory = TempDirectory.Create();
        var configStore = new RunnerConfigStore(Path.Combine(directory.Path, "settings.json"));
        await configStore.SaveAsync(new RunnerConfig
        {
            WindowPlacement = new WindowPlacement
            {
                X = 24,
                Y = 32,
                Width = 1220,
                Height = 780,
                IsMaximized = true
            },
            Runners = [CreateRunnerDefinition("Test app", directory.Path)]
        });
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.ToggleAlwaysOnTopCommand.ExecuteAsync(null);

        var savedConfig = await configStore.LoadAsync();
        Assert.NotNull(savedConfig.WindowPlacement);
        Assert.Equal(24, savedConfig.WindowPlacement.X);
        Assert.Equal(32, savedConfig.WindowPlacement.Y);
        Assert.Equal(1220, savedConfig.WindowPlacement.Width);
        Assert.Equal(780, savedConfig.WindowPlacement.Height);
        Assert.True(savedConfig.WindowPlacement.IsMaximized);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenUpdateIsAvailable_ShowsUpdateAction()
    {
        using var directory = TempDirectory.Create();
        var updateService = new FakeAppUpdateService(
            AppUpdateCheckResult.Available("1.0.0", "1.1.0", isDownloaded: false));
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false),
            updateService);

        await viewModel.CheckForUpdatesAsync();

        Assert.True(viewModel.IsUpdateAvailable);
        Assert.True(viewModel.IsUpdateActionVisible);
        Assert.True(viewModel.ApplyUpdateCommand.CanExecute(null));
        Assert.Equal("1.1.0", viewModel.AvailableUpdateVersion);
        Assert.Equal("Update 1.1.0", viewModel.UpdateActionText);
        Assert.Equal("Update 1.1.0 is available.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenDownloadedUpdateIsPending_ShowsRestartAction()
    {
        using var directory = TempDirectory.Create();
        var updateService = new FakeAppUpdateService(
            AppUpdateCheckResult.Available("1.0.0", "1.1.0", isDownloaded: true));
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false),
            updateService);

        await viewModel.CheckForUpdatesAsync();

        Assert.True(viewModel.IsUpdateDownloaded);
        Assert.Equal("Restart to update", viewModel.UpdateActionText);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenUpdatesUnsupported_HidesUpdateAction()
    {
        using var directory = TempDirectory.Create();
        var updateService = new FakeAppUpdateService(
            AppUpdateCheckResult.Unsupported("Updates are unavailable."));
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false),
            updateService);

        await viewModel.CheckForUpdatesAsync();

        Assert.False(viewModel.IsUpdateAvailable);
        Assert.False(viewModel.IsUpdateActionVisible);
        Assert.False(viewModel.ApplyUpdateCommand.CanExecute(null));
    }

    [Fact]
    public async Task ApplyUpdateCommand_WhenUpdateIsAvailable_AppliesUpdate()
    {
        using var directory = TempDirectory.Create();
        var updateService = new FakeAppUpdateService(
            AppUpdateCheckResult.Available("1.0.0", "1.1.0", isDownloaded: false));
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false),
            updateService);
        await viewModel.CheckForUpdatesAsync();

        await viewModel.ApplyUpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, updateService.ApplyCount);
        Assert.Equal("Restarting to apply update...", viewModel.StatusMessage);
        Assert.False(viewModel.IsUpdating);
    }

    [Fact]
    public async Task StartRunnerCommand_OperatesOnPassedRunnerInsteadOfSelectedRunner()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var factory = new RecordingRunnerFactory(RunnerStatus.Stopped);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.StartRunnerCommand.ExecuteAsync(viewModel.Runners[1]);

        Assert.Equal(0, factory.Created[0].StartCount);
        Assert.Equal(1, factory.Created[1].StartCount);
        Assert.Same(viewModel.Runners[1], viewModel.SelectedRunner);
    }

    [Fact]
    public async Task RunRunnerCommand_WhenRunnerIsStopped_StartsPassedRunner()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var factory = new RecordingRunnerFactory(RunnerStatus.Stopped);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.RunRunnerCommand.ExecuteAsync(viewModel.Runners[1]);

        Assert.Equal(0, factory.Created[0].StartCount);
        Assert.Equal(1, factory.Created[1].StartCount);
        Assert.Equal(0, factory.Created[1].RestartCount);
        Assert.Same(viewModel.Runners[1], viewModel.SelectedRunner);
    }

    [Fact]
    public async Task RunRunnerCommand_WhenRunnerIsRunning_RestartsPassedRunner()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var factory = new RecordingRunnerFactory(RunnerStatus.Running);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.RunRunnerCommand.ExecuteAsync(viewModel.Runners[1]);

        Assert.Equal(0, factory.Created[0].RestartCount);
        Assert.Equal(1, factory.Created[1].RestartCount);
        Assert.Same(viewModel.Runners[1], viewModel.SelectedRunner);
    }

    [Fact]
    public async Task StopRunnerCommand_OperatesOnPassedRunnerInsteadOfSelectedRunner()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var factory = new RecordingRunnerFactory(RunnerStatus.Running);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.StopRunnerCommand.ExecuteAsync(viewModel.Runners[1]);

        Assert.Equal(0, factory.Created[0].StopCount);
        Assert.Equal(1, factory.Created[1].StopCount);
        Assert.Same(viewModel.Runners[1], viewModel.SelectedRunner);
    }

    [Fact]
    public async Task RestartRunnerCommand_OperatesOnPassedRunnerInsteadOfSelectedRunner()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var factory = new RecordingRunnerFactory(RunnerStatus.Stopped);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.RestartRunnerCommand.ExecuteAsync(viewModel.Runners[1]);

        Assert.Equal(0, factory.Created[0].RestartCount);
        Assert.Equal(1, factory.Created[1].RestartCount);
        Assert.Same(viewModel.Runners[1], viewModel.SelectedRunner);
    }

    [Fact]
    public async Task SelectedRunnerCommands_DelegateToSelectedRunner()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var factory = new RecordingRunnerFactory(RunnerStatus.Stopped);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.StartSelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, factory.Created[0].StartCount);
        Assert.Equal(0, factory.Created[1].StartCount);
    }

    [Fact]
    public async Task RunSelectedCommand_DelegatesToSelectedRunner()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var factory = new RecordingRunnerFactory(RunnerStatus.Running);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.RunSelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, factory.Created[0].RestartCount);
        Assert.Equal(0, factory.Created[1].RestartCount);
    }

    [Fact]
    public async Task RemoveSelectedCommand_WhenConfirmed_RemovesSelectedRunnerAndSavesConfig()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var confirmation = new FakeRunnerRemovalConfirmation(true);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            confirmation);
        await viewModel.LoadAsync();

        await viewModel.RemoveSelectedCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Runners);
        Assert.Equal("Second runner", viewModel.Runners[0].DisplayName);
        Assert.Equal("Second runner", viewModel.SelectedRunner?.DisplayName);
        Assert.Equal("First runner", confirmation.LastRunnerName);

        var savedConfig = await configStore.LoadAsync();
        var savedRunner = Assert.Single(savedConfig.Runners);
        Assert.Equal("Second runner", savedRunner.DisplayName);
    }

    [Fact]
    public async Task RemoveSelectedCommand_WhenCanceled_LeavesRunnerAndConfigUnchanged()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var confirmation = new FakeRunnerRemovalConfirmation(false);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            confirmation);
        await viewModel.LoadAsync();

        await viewModel.RemoveSelectedCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Runners.Count);
        Assert.Equal("First runner", viewModel.SelectedRunner?.DisplayName);
        Assert.Equal("First runner", confirmation.LastRunnerName);
        Assert.Equal("Remove canceled.", viewModel.StatusMessage);

        var savedConfig = await configStore.LoadAsync();
        Assert.Equal(2, savedConfig.Runners.Count);
    }

    [Fact]
    public async Task RemoveSelectedCommand_WhenRunnerIsRunning_StopsAndDisposesAfterConfirmation()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("Running runner", directory.Path));
        var confirmation = new FakeRunnerRemovalConfirmation(true);
        var factory = new RecordingRunnerFactory(RunnerStatus.Running);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            confirmation);
        await viewModel.LoadAsync();
        var runner = Assert.Single(factory.Created);

        await viewModel.RemoveSelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, runner.StopCount);
        Assert.Equal(1, runner.DisposeCount);
        Assert.Empty(viewModel.Runners);
        Assert.Null(viewModel.SelectedRunner);
    }

    [Fact]
    public void RemoveSelectedCommand_WhenNoRunnerSelected_IsNotExecutable()
    {
        using var directory = TempDirectory.Create();
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(directory.Path),
            new FakeRunnerRemovalConfirmation(true));

        Assert.False(viewModel.RemoveSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task BrowseWorkingDirectoryCommand_WhenFolderSelected_UpdatesWorkingDirectory()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var selectedPath = Path.Combine(directory.Path, "selected");
        Directory.CreateDirectory(selectedPath);
        var picker = new FakeWorkingDirectoryPicker(selectedPath);
        var viewModel = new MainWindowViewModel(configStore, new RunnerFactory(), picker);
        await viewModel.LoadAsync();

        await viewModel.BrowseWorkingDirectoryCommand.ExecuteAsync(null);

        Assert.Equal(selectedPath, viewModel.SelectedRunner?.WorkingDirectory);
        Assert.Equal(directory.Path, picker.LastCurrentPath);
        Assert.Equal("Updated working directory.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task BrowseWorkingDirectoryCommand_WhenPickerCanceled_LeavesWorkingDirectoryUnchanged()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var picker = new FakeWorkingDirectoryPicker(null);
        var viewModel = new MainWindowViewModel(configStore, new RunnerFactory(), picker);
        await viewModel.LoadAsync();

        await viewModel.BrowseWorkingDirectoryCommand.ExecuteAsync(null);

        Assert.Equal(directory.Path, viewModel.SelectedRunner?.WorkingDirectory);
        Assert.Equal("Folder selection canceled.", viewModel.StatusMessage);
    }

    [Fact]
    public void BrowseWorkingDirectoryCommand_WhenNoRunnerSelected_IsNotExecutable()
    {
        using var directory = TempDirectory.Create();
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(directory.Path));

        Assert.False(viewModel.BrowseWorkingDirectoryCommand.CanExecute(null));
    }

    private static RunnerConfigStore CreateConfigStore(
        string workingDirectory,
        params RunnerDefinition[] runners)
    {
        var configPath = Path.Combine(workingDirectory, "settings.json");
        var configStore = new RunnerConfigStore(configPath);

        if (runners.Length == 0)
        {
            runners =
            [
                CreateRunnerDefinition("Test app", workingDirectory)
            ];
        }

        configStore.SaveAsync(new RunnerConfig
        {
            Runners = runners.ToList()
        }).GetAwaiter().GetResult();

        return configStore;
    }

    private static RunnerDefinition CreateRunnerDefinition(string displayName, string workingDirectory)
    {
        return new RunnerDefinition
        {
            DisplayName = displayName,
            Type = RunnerType.DotNetProject,
            WorkingDirectory = workingDirectory
        };
    }

    private sealed class FakeWorkingDirectoryPicker : IWorkingDirectoryPicker
    {
        private readonly string? _selectedPath;

        public FakeWorkingDirectoryPicker(string? selectedPath)
        {
            _selectedPath = selectedPath;
        }

        public string? LastCurrentPath { get; private set; }

        public Task<string?> PickWorkingDirectoryAsync(
            string? currentPath,
            CancellationToken cancellationToken = default)
        {
            LastCurrentPath = currentPath;
            return Task.FromResult(_selectedPath);
        }
    }

    private sealed class FakeRunnerRemovalConfirmation : IRunnerRemovalConfirmation
    {
        private readonly bool _confirmed;

        public FakeRunnerRemovalConfirmation(bool confirmed)
        {
            _confirmed = confirmed;
        }

        public string? LastRunnerName { get; private set; }

        public Task<bool> ConfirmRemoveAsync(
            string runnerName,
            CancellationToken cancellationToken = default)
        {
            LastRunnerName = runnerName;
            return Task.FromResult(_confirmed);
        }
    }

    private sealed class FakeAppUpdateService : IAppUpdateService
    {
        private readonly AppUpdateCheckResult _checkResult;

        public FakeAppUpdateService(AppUpdateCheckResult checkResult)
        {
            _checkResult = checkResult;
        }

        public int ApplyCount { get; private set; }

        public Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_checkResult);
        }

        public Task ApplyUpdateAndRestartAsync(
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRunnerFactory : IRunnerFactory
    {
        private readonly RunnerStatus _initialStatus;

        public RecordingRunnerFactory(RunnerStatus initialStatus)
        {
            _initialStatus = initialStatus;
        }

        public List<RecordingRunner> Created { get; } = [];

        public IRunner Create(RunnerDefinition definition)
        {
            var runner = new RecordingRunner(definition, _initialStatus);
            Created.Add(runner);
            return runner;
        }
    }

    private sealed class RecordingRunner : IRunner
    {
        public RecordingRunner(RunnerDefinition definition, RunnerStatus initialStatus)
        {
            Definition = definition;
            Status = initialStatus;
        }

        public event EventHandler<RunnerStatus>? StatusChanged;

        public RunnerDefinition Definition { get; }

        public RunnerStatus Status { get; private set; }

        public int? ProcessId => Status == RunnerStatus.Running ? 123 : null;

        public RunnerFailureDetails? LastFailure => null;

        public int StopCount { get; private set; }

        public int StartCount { get; private set; }

        public int RestartCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            Status = RunnerStatus.Running;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            Status = RunnerStatus.Stopped;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public async Task RestartAsync(CancellationToken cancellationToken = default)
        {
            RestartCount++;
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
