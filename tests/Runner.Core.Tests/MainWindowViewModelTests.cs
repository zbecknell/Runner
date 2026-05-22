using Runner.App.Services;
using Runner.App.ViewModels;
using Runner.Core.Config;
using Runner.Core.Runners;

namespace Runner.Core.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_DefaultsToCompactDashboardSize()
    {
        using var directory = TempDirectory.Create();
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));

        Assert.Equal(200, viewModel.WindowMinWidth);
        Assert.Equal(112, viewModel.WindowMinHeight);
        Assert.False(viewModel.IsSettingsDirty);
    }

    [Fact]
    public void OpenSettingsCommand_RequestsSettingsOpen()
    {
        using var directory = TempDirectory.Create();
        var viewModel = new MainWindowViewModel(
            new RunnerConfigStore(Path.Combine(directory.Path, "settings.json")),
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        var openRequestCount = 0;
        viewModel.SettingsOpenRequested += (_, _) => openRequestCount++;

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, openRequestCount);
    }

    [Fact]
    public async Task OpenRunnerDetailsCommand_SelectsRunnerAndRequestsDetailsOpen()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First runner", directory.Path),
            CreateRunnerDefinition("Second runner", directory.Path));
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();
        var requestedRunners = new List<RunnerViewModel>();
        viewModel.RunnerDetailsOpenRequested += (_, runner) => requestedRunners.Add(runner);

        viewModel.OpenRunnerDetailsCommand.Execute(viewModel.Runners[1]);

        Assert.Same(viewModel.Runners[1], viewModel.SelectedRunner);
        Assert.Same(viewModel.Runners[1], Assert.Single(requestedRunners));
    }

    [Fact]
    public async Task AddRunnerCommand_FromEmptyDashboard_CreatesRunnerSelectsItSavesAndRequestsSettingsOpen()
    {
        using var directory = TempDirectory.Create();
        var configStore = new RunnerConfigStore(Path.Combine(directory.Path, "settings.json"));
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        var openRequestCount = 0;
        viewModel.SettingsOpenRequested += (_, _) => openRequestCount++;
        await viewModel.LoadAsync();

        await viewModel.AddRunnerCommand.ExecuteAsync(null);

        var runner = Assert.Single(viewModel.Runners);
        Assert.Same(runner, viewModel.SelectedRunner);
        Assert.False(viewModel.IsSettingsDirty);
        Assert.Equal(1, openRequestCount);

        var savedConfig = await configStore.LoadAsync();
        Assert.Single(savedConfig.Runners);
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
            CleanBeforeRestore = true,
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
        Assert.False(viewModel.IsSettingsDirty);
        Assert.Equal("Cloned runner.", viewModel.StatusMessage);
        Assert.Equal("API (Clone)", clone.DisplayName);
        Assert.Equal(RunnerType.DotNetProject, clone.Type);
        Assert.Equal(directory.Path, clone.WorkingDirectory);
        Assert.Equal("Api.csproj", clone.Command);
        Assert.Equal("--urls http://localhost:5005", clone.Arguments);
        Assert.True(clone.CleanBeforeRestore);
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
        Assert.True(savedConfig.Runners[1].CleanBeforeRestore);
    }

    [Fact]
    public async Task MoveRunner_ReordersProjectsPreservesSelectionAndMarksSettingsDirty()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First", directory.Path),
            CreateRunnerDefinition("Second", directory.Path),
            CreateRunnerDefinition("Third", directory.Path));
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();
        var selectedRunner = viewModel.Runners[0];

        var moved = viewModel.MoveRunner(selectedRunner, 2);

        Assert.True(moved);
        Assert.True(viewModel.IsSettingsDirty);
        Assert.Same(selectedRunner, viewModel.SelectedRunner);
        Assert.Equal(["Second", "Third", "First"], viewModel.Runners.Select(runner => runner.DisplayName));
        Assert.Equal("Reordered projects.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task MoveRunnerCommands_MovePassedRunnerAndRespectBoundaries()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First", directory.Path),
            CreateRunnerDefinition("Second", directory.Path),
            CreateRunnerDefinition("Third", directory.Path));
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        var firstRunner = viewModel.Runners[0];
        var secondRunner = viewModel.Runners[1];

        Assert.False(viewModel.MoveRunnerUpCommand.CanExecute(firstRunner));
        Assert.True(viewModel.MoveRunnerDownCommand.CanExecute(firstRunner));

        viewModel.SelectedRunner = viewModel.Runners[1];

        Assert.True(viewModel.MoveRunnerUpCommand.CanExecute(secondRunner));
        Assert.True(viewModel.MoveRunnerDownCommand.CanExecute(secondRunner));

        viewModel.MoveRunnerUpCommand.Execute(secondRunner);

        Assert.Equal(["Second", "First", "Third"], viewModel.Runners.Select(runner => runner.DisplayName));
        Assert.Same(viewModel.Runners[0], viewModel.SelectedRunner);
        Assert.False(viewModel.MoveRunnerUpCommand.CanExecute(secondRunner));
        Assert.True(viewModel.MoveRunnerDownCommand.CanExecute(secondRunner));

        viewModel.MoveRunnerDownCommand.Execute(secondRunner);
        viewModel.MoveRunnerDownCommand.Execute(secondRunner);

        Assert.Equal(["First", "Third", "Second"], viewModel.Runners.Select(runner => runner.DisplayName));
        Assert.Same(viewModel.Runners[2], viewModel.SelectedRunner);
        Assert.True(viewModel.MoveRunnerUpCommand.CanExecute(secondRunner));
        Assert.False(viewModel.MoveRunnerDownCommand.CanExecute(secondRunner));
    }

    [Fact]
    public async Task SaveConfigCommand_PersistsReorderedProjects()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First", directory.Path),
            CreateRunnerDefinition("Second", directory.Path),
            CreateRunnerDefinition("Third", directory.Path));
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        viewModel.MoveRunner(viewModel.Runners[2], 0);
        await viewModel.SaveConfigCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsSettingsDirty);
        var savedConfig = await configStore.LoadAsync();
        Assert.Equal(["Third", "First", "Second"], savedConfig.Runners.Select(runner => runner.DisplayName));
    }

    [Fact]
    public async Task DiscardSettingsChanges_RestoresLastSavedProjectOrder()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(
            directory.Path,
            CreateRunnerDefinition("First", directory.Path),
            CreateRunnerDefinition("Second", directory.Path),
            CreateRunnerDefinition("Third", directory.Path));
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        viewModel.MoveRunner(viewModel.Runners[2], 0);

        viewModel.DiscardSettingsChanges();

        Assert.False(viewModel.IsSettingsDirty);
        Assert.Equal(["First", "Second", "Third"], viewModel.Runners.Select(runner => runner.DisplayName));
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
    public async Task RunnerFieldEdit_MarksSettingsDirtyAndSaveClearsDirty()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        viewModel.SelectedRunner!.DisplayName = "Updated app";

        Assert.True(viewModel.IsSettingsDirty);

        await viewModel.SaveConfigCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsSettingsDirty);
        var savedConfig = await configStore.LoadAsync();
        Assert.Equal("Updated app", savedConfig.Runners[0].DisplayName);
    }

    [Fact]
    public async Task CleanBeforeRestoreEdit_MarksSettingsDirtyAndSavePersists()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        viewModel.SelectedRunner!.CleanBeforeRestore = true;

        Assert.True(viewModel.IsSettingsDirty);

        await viewModel.SaveConfigCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsSettingsDirty);
        var savedConfig = await configStore.LoadAsync();
        Assert.True(savedConfig.Runners[0].CleanBeforeRestore);
    }

    [Fact]
    public async Task RunnerTypeEdit_MarksSettingsDirtyAndSavePersists()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        viewModel.SelectedRunner!.Type = RunnerType.DotNetProjectBuild;

        Assert.True(viewModel.IsSettingsDirty);

        await viewModel.SaveConfigCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsSettingsDirty);
        var savedConfig = await configStore.LoadAsync();
        Assert.Equal(RunnerType.DotNetProjectBuild, savedConfig.Runners[0].Type);
    }

    [Fact]
    public async Task RunRunnerCommand_ForBuildOnlyRunner_StartsBuildAndShowsBuiltStatus()
    {
        using var directory = TempDirectory.Create();
        var definition = CreateRunnerDefinition("Build runner", directory.Path);
        definition.Type = RunnerType.DotNetProjectBuild;
        var configStore = CreateConfigStore(directory.Path, definition);
        var factory = new RecordingRunnerFactory(RunnerStatus.Stopped);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.RunRunnerCommand.ExecuteAsync(viewModel.Runners[0]);

        Assert.Equal(1, factory.Created[0].BuildCount);
        Assert.Equal(0, factory.Created[0].StartCount);
        Assert.Equal(0, factory.Created[0].RestartCount);
        Assert.Equal("Built Build runner.", viewModel.StatusMessage);
        Assert.NotNull(viewModel.Runners[0].LastFinishedAt);
        Assert.Equal("Finished", viewModel.Runners[0].DashboardStatusText);
    }

    [Fact]
    public async Task AddRunnerCommand_DefaultsCleanBeforeRestoreOff()
    {
        using var directory = TempDirectory.Create();
        var configStore = new RunnerConfigStore(Path.Combine(directory.Path, "settings.json"));
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.AddRunnerCommand.ExecuteAsync(null);

        Assert.False(viewModel.SelectedRunner!.CleanBeforeRestore);
        var savedConfig = await configStore.LoadAsync();
        Assert.False(savedConfig.Runners[0].CleanBeforeRestore);
    }

    [Fact]
    public async Task DiscardSettingsChanges_RestoresLastSavedEditableFields()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();
        var runner = viewModel.SelectedRunner!;

        runner.DisplayName = "Dirty name";
        runner.Type = RunnerType.DotNetProjectBuild;
        runner.WorkingDirectory = Path.Combine(directory.Path, "dirty");
        runner.Command = "Dirty.csproj";
        runner.Arguments = "--dirty";
        runner.CleanBeforeRestore = true;
        runner.EnvironmentVariablesText = "DIRTY=true";

        viewModel.DiscardSettingsChanges();

        Assert.False(viewModel.IsSettingsDirty);
        Assert.Equal("Test app", runner.DisplayName);
        Assert.Equal(RunnerType.DotNetProject, runner.Type);
        Assert.Equal(directory.Path, runner.WorkingDirectory);
        Assert.Equal("", runner.Command);
        Assert.Equal("", runner.Arguments);
        Assert.False(runner.CleanBeforeRestore);
        Assert.Equal("", runner.EnvironmentVariablesText);
    }

    [Fact]
    public async Task PreferenceSave_WhenSettingsAreDirty_PreservesLastSavedRunnerConfig()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        viewModel.SelectedRunner!.DisplayName = "Unsaved name";
        await viewModel.ToggleAlwaysOnTopCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsSettingsDirty);
        var savedConfig = await configStore.LoadAsync();
        Assert.True(savedConfig.AlwaysOnTop);
        Assert.Equal("Test app", savedConfig.Runners[0].DisplayName);
    }

    [Fact]
    public async Task LoadAsync_WhenGeneralPreferencesAreMissing_DefaultsToPathsShownAndFullOpacity()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));

        await viewModel.LoadAsync();

        Assert.True(viewModel.ShowProjectPaths);
        Assert.Equal(1.0, viewModel.RunnerOpacity);
        Assert.Equal(100, viewModel.RunnerOpacityPercent);
        Assert.Equal("100%", viewModel.RunnerOpacityDisplayText);
    }

    [Fact]
    public async Task ShowProjectPathsEdit_SavesImmediatelyAndPreservesDirtyRunnerConfig()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();
        viewModel.SelectedRunner!.DisplayName = "Unsaved name";

        viewModel.ShowProjectPaths = false;

        Assert.True(viewModel.IsSettingsDirty);
        var savedConfig = await WaitForSavedConfigAsync(
            configStore,
            config => !config.ShowProjectPaths);
        Assert.False(savedConfig.ShowProjectPaths);
        Assert.Equal("Test app", savedConfig.Runners[0].DisplayName);
    }

    [Fact]
    public async Task RunnerOpacityEdit_SavesImmediatelyAndPreservesDirtyRunnerConfig()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();
        viewModel.SelectedRunner!.DisplayName = "Unsaved name";

        viewModel.RunnerOpacityPercent = 55;

        Assert.True(viewModel.IsSettingsDirty);
        Assert.Equal(0.55, viewModel.RunnerOpacity, 2);
        Assert.Equal("55%", viewModel.RunnerOpacityDisplayText);
        var savedConfig = await WaitForSavedConfigAsync(
            configStore,
            config => Math.Abs(config.RunnerOpacity - 0.55) < 0.001);
        Assert.Equal(0.55, savedConfig.RunnerOpacity, 2);
        Assert.Equal("Test app", savedConfig.Runners[0].DisplayName);
    }

    [Fact]
    public async Task LoadAsync_ClampsRunnerOpacity()
    {
        using var directory = TempDirectory.Create();
        var configStore = new RunnerConfigStore(Path.Combine(directory.Path, "settings.json"));
        await configStore.SaveAsync(new RunnerConfig
        {
            RunnerOpacity = 0.01,
            Runners = [CreateRunnerDefinition("Test app", directory.Path)]
        });
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));

        await viewModel.LoadAsync();

        Assert.Equal(0.1, viewModel.RunnerOpacity);
        Assert.Equal(10, viewModel.RunnerOpacityPercent);
        Assert.Equal("10%", viewModel.RunnerOpacityDisplayText);
    }

    [Fact]
    public async Task ToggleLogOrderCommand_TogglesSavesAndPreservesDirtyRunnerConfig()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();
        viewModel.SelectedRunner!.DisplayName = "Unsaved name";

        await viewModel.ToggleLogOrderCommand.ExecuteAsync(null);

        Assert.False(viewModel.ShowNewestLogsFirst);
        Assert.True(viewModel.IsSettingsDirty);
        var savedConfig = await configStore.LoadAsync();
        Assert.False(savedConfig.ShowNewestLogsFirst);
        Assert.Equal("Test app", savedConfig.Runners[0].DisplayName);
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
    public async Task LoadAsync_LoadsSettingsWindowPlacement()
    {
        using var directory = TempDirectory.Create();
        var placement = new WindowPlacement
        {
            X = 360,
            Y = 220,
            Width = 980,
            Height = 640,
            IsMaximized = true
        };
        var configStore = new RunnerConfigStore(Path.Combine(directory.Path, "settings.json"));
        await configStore.SaveAsync(new RunnerConfig
        {
            SettingsWindowPlacement = placement,
            Runners = [CreateRunnerDefinition("Test app", directory.Path)]
        });
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));

        await viewModel.LoadAsync();

        Assert.NotNull(viewModel.SettingsWindowPlacement);
        Assert.Equal(360, viewModel.SettingsWindowPlacement.X);
        Assert.Equal(220, viewModel.SettingsWindowPlacement.Y);
        Assert.Equal(980, viewModel.SettingsWindowPlacement.Width);
        Assert.Equal(640, viewModel.SettingsWindowPlacement.Height);
        Assert.True(viewModel.SettingsWindowPlacement.IsMaximized);
    }

    [Fact]
    public async Task LoadAsync_LoadsDetailsWindowPlacement()
    {
        using var directory = TempDirectory.Create();
        var placement = new WindowPlacement
        {
            X = 420,
            Y = 260,
            Width = 1040,
            Height = 700,
            IsMaximized = true
        };
        var configStore = new RunnerConfigStore(Path.Combine(directory.Path, "settings.json"));
        await configStore.SaveAsync(new RunnerConfig
        {
            DetailsWindowPlacement = placement,
            Runners = [CreateRunnerDefinition("Test app", directory.Path)]
        });
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));

        await viewModel.LoadAsync();

        Assert.NotNull(viewModel.DetailsWindowPlacement);
        Assert.Equal(420, viewModel.DetailsWindowPlacement.X);
        Assert.Equal(260, viewModel.DetailsWindowPlacement.Y);
        Assert.Equal(1040, viewModel.DetailsWindowPlacement.Width);
        Assert.Equal(700, viewModel.DetailsWindowPlacement.Height);
        Assert.True(viewModel.DetailsWindowPlacement.IsMaximized);
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
    public async Task SaveSettingsWindowPlacementAsync_PersistsPlacementWithoutDroppingConfig()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();
        viewModel.SelectedRunner!.DisplayName = "Unsaved name";

        await viewModel.SaveSettingsWindowPlacementAsync(new WindowPlacement
        {
            X = 72,
            Y = 96,
            Width = 1020,
            Height = 680,
            IsMaximized = false
        });

        Assert.True(viewModel.IsSettingsDirty);
        var savedConfig = await configStore.LoadAsync();
        Assert.Single(savedConfig.Runners);
        Assert.Equal("Test app", savedConfig.Runners[0].DisplayName);
        Assert.NotNull(savedConfig.SettingsWindowPlacement);
        Assert.Equal(72, savedConfig.SettingsWindowPlacement.X);
        Assert.Equal(96, savedConfig.SettingsWindowPlacement.Y);
        Assert.Equal(1020, savedConfig.SettingsWindowPlacement.Width);
        Assert.Equal(680, savedConfig.SettingsWindowPlacement.Height);
        Assert.False(savedConfig.SettingsWindowPlacement.IsMaximized);
    }

    [Fact]
    public async Task SaveDetailsWindowPlacementAsync_PersistsPlacementWithoutDroppingConfig()
    {
        using var directory = TempDirectory.Create();
        var configStore = CreateConfigStore(directory.Path);
        var viewModel = new MainWindowViewModel(
            configStore,
            new RunnerFactory(),
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();
        viewModel.SelectedRunner!.DisplayName = "Unsaved name";

        await viewModel.SaveDetailsWindowPlacementAsync(new WindowPlacement
        {
            X = 88,
            Y = 112,
            Width = 1120,
            Height = 740,
            IsMaximized = false
        });

        Assert.True(viewModel.IsSettingsDirty);
        var savedConfig = await configStore.LoadAsync();
        Assert.Single(savedConfig.Runners);
        Assert.Equal("Test app", savedConfig.Runners[0].DisplayName);
        Assert.NotNull(savedConfig.DetailsWindowPlacement);
        Assert.Equal(88, savedConfig.DetailsWindowPlacement.X);
        Assert.Equal(112, savedConfig.DetailsWindowPlacement.Y);
        Assert.Equal(1120, savedConfig.DetailsWindowPlacement.Width);
        Assert.Equal(740, savedConfig.DetailsWindowPlacement.Height);
        Assert.False(savedConfig.DetailsWindowPlacement.IsMaximized);
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
    public async Task SaveConfigAsync_PreservesLoadedSettingsWindowPlacement()
    {
        using var directory = TempDirectory.Create();
        var configStore = new RunnerConfigStore(Path.Combine(directory.Path, "settings.json"));
        await configStore.SaveAsync(new RunnerConfig
        {
            SettingsWindowPlacement = new WindowPlacement
            {
                X = 260,
                Y = 180,
                Width = 940,
                Height = 620,
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
        Assert.NotNull(savedConfig.SettingsWindowPlacement);
        Assert.Equal(260, savedConfig.SettingsWindowPlacement.X);
        Assert.Equal(180, savedConfig.SettingsWindowPlacement.Y);
        Assert.Equal(940, savedConfig.SettingsWindowPlacement.Width);
        Assert.Equal(620, savedConfig.SettingsWindowPlacement.Height);
        Assert.True(savedConfig.SettingsWindowPlacement.IsMaximized);
    }

    [Fact]
    public async Task SaveConfigAsync_PreservesLoadedDetailsWindowPlacement()
    {
        using var directory = TempDirectory.Create();
        var configStore = new RunnerConfigStore(Path.Combine(directory.Path, "settings.json"));
        await configStore.SaveAsync(new RunnerConfig
        {
            DetailsWindowPlacement = new WindowPlacement
            {
                X = 320,
                Y = 240,
                Width = 980,
                Height = 660,
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
        Assert.NotNull(savedConfig.DetailsWindowPlacement);
        Assert.Equal(320, savedConfig.DetailsWindowPlacement.X);
        Assert.Equal(240, savedConfig.DetailsWindowPlacement.Y);
        Assert.Equal(980, savedConfig.DetailsWindowPlacement.Width);
        Assert.Equal(660, savedConfig.DetailsWindowPlacement.Height);
        Assert.True(savedConfig.DetailsWindowPlacement.IsMaximized);
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
    public async Task RunRunnerCommand_WhenRunnerIsRunning_StopsPassedRunner()
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

        Assert.Equal(0, factory.Created[0].StopCount);
        Assert.Equal(1, factory.Created[1].StopCount);
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
        var factory = new RecordingRunnerFactory(RunnerStatus.Running);
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
    public async Task CleanRunnerCommand_OperatesOnPassedRunnerInsteadOfSelectedRunner()
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

        await viewModel.CleanRunnerCommand.ExecuteAsync(viewModel.Runners[1]);

        Assert.Equal(0, factory.Created[0].CleanCount);
        Assert.Equal(1, factory.Created[1].CleanCount);
        Assert.Same(viewModel.Runners[1], viewModel.SelectedRunner);
        Assert.Equal("Cleaned Second runner.", viewModel.StatusMessage);
        Assert.Null(viewModel.Runners[1].LastFinishedAt);
    }

    [Fact]
    public async Task BuildRunnerCommand_OperatesOnPassedRunnerInsteadOfSelectedRunner()
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

        await viewModel.BuildRunnerCommand.ExecuteAsync(viewModel.Runners[1]);

        Assert.Equal(0, factory.Created[0].BuildCount);
        Assert.Equal(1, factory.Created[1].BuildCount);
        Assert.Same(viewModel.Runners[1], viewModel.SelectedRunner);
        Assert.Equal("Built Second runner.", viewModel.StatusMessage);
        Assert.Null(viewModel.Runners[1].LastFinishedAt);
    }

    [Fact]
    public async Task BuildRunnerCommand_ForBuildOnlyRunner_SetsFinishedState()
    {
        using var directory = TempDirectory.Create();
        var definition = CreateRunnerDefinition("Build runner", directory.Path);
        definition.Type = RunnerType.DotNetProjectBuild;
        var configStore = CreateConfigStore(directory.Path, definition);
        var factory = new RecordingRunnerFactory(RunnerStatus.Stopped);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.BuildRunnerCommand.ExecuteAsync(viewModel.Runners[0]);

        Assert.NotNull(viewModel.Runners[0].LastFinishedAt);
        Assert.Equal("Finished", viewModel.Runners[0].DashboardStatusText);
    }

    [Fact]
    public async Task CleanRunnerCommand_ForBuildOnlyRunner_SetsFinishedState()
    {
        using var directory = TempDirectory.Create();
        var definition = CreateRunnerDefinition("Build runner", directory.Path);
        definition.Type = RunnerType.DotNetProjectBuild;
        var configStore = CreateConfigStore(directory.Path, definition);
        var factory = new RecordingRunnerFactory(RunnerStatus.Stopped);
        var viewModel = new MainWindowViewModel(
            configStore,
            factory,
            new FakeWorkingDirectoryPicker(null),
            new FakeRunnerRemovalConfirmation(false));
        await viewModel.LoadAsync();

        await viewModel.CleanRunnerCommand.ExecuteAsync(viewModel.Runners[0]);

        Assert.NotNull(viewModel.Runners[0].LastFinishedAt);
        Assert.Equal("Finished", viewModel.Runners[0].DashboardStatusText);
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

        Assert.Equal(1, factory.Created[0].StopCount);
        Assert.Equal(0, factory.Created[1].StopCount);
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

    private static async Task<RunnerConfig> WaitForSavedConfigAsync(
        RunnerConfigStore configStore,
        Func<RunnerConfig, bool> predicate)
    {
        RunnerConfig config = new();

        for (var attempt = 0; attempt < 20; attempt++)
        {
            config = await configStore.LoadAsync();

            if (predicate(config))
            {
                return config;
            }

            await Task.Delay(50);
        }

        return config;
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

        public IReadOnlyList<string> LogLines => [];

        public void ClearLogs()
        {
        }

        public int StopCount { get; private set; }

        public int CleanCount { get; private set; }

        public int BuildCount { get; private set; }

        public int StartCount { get; private set; }

        public int RestartCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task CleanAsync(CancellationToken cancellationToken = default)
        {
            CleanCount++;
            Status = RunnerStatus.Stopped;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public Task BuildAsync(CancellationToken cancellationToken = default)
        {
            BuildCount++;
            Status = RunnerStatus.Stopped;
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            Status = Definition.Type == RunnerType.DotNetProjectBuild
                ? RunnerStatus.Stopped
                : RunnerStatus.Running;
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
