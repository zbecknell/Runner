using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Runner.App.Services;
using Runner.Core.Config;
using Runner.Core.Runners;

namespace Runner.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly RunnerConfigStore _configStore;
    private readonly IRunnerFactory _runnerFactory;
    private readonly IWorkingDirectoryPicker _workingDirectoryPicker;
    private readonly IRunnerRemovalConfirmation _runnerRemovalConfirmation;
    private readonly IAppUpdateService _appUpdateService;
    private bool _alwaysOnTop;
    private string? _availableUpdateVersion;
    private bool _isCheckingForUpdates;
    private bool _isLoading;
    private bool _isUpdateAvailable;
    private bool _isUpdateDownloaded;
    private bool _isUpdating;
    private bool _isSettingsDirty;
    private bool _suppressSettingsDirtyTracking;
    private bool _suppressAlwaysOnTopSave;
    private List<RunnerDefinition> _lastSavedDefinitions = [];
    private RunnerViewModel? _selectedRunner;
    private string _statusMessage = "Loading configuration...";
    private int _updateProgress;
    private WindowPlacement? _windowPlacement;

    public MainWindowViewModel()
        : this(
            new RunnerConfigStore(RunnerConfigStore.GetDefaultConfigPath()),
            new RunnerFactory(),
            NullWorkingDirectoryPicker.Instance,
            NullRunnerRemovalConfirmation.Instance,
            NullAppUpdateService.Instance)
    {
        _ = LoadAsync();
    }

    public MainWindowViewModel(
        RunnerConfigStore configStore,
        IRunnerFactory runnerFactory,
        IWorkingDirectoryPicker workingDirectoryPicker)
        : this(
            configStore,
            runnerFactory,
            workingDirectoryPicker,
            NullRunnerRemovalConfirmation.Instance,
            NullAppUpdateService.Instance)
    {
    }

    public MainWindowViewModel(
        RunnerConfigStore configStore,
        IRunnerFactory runnerFactory,
        IWorkingDirectoryPicker workingDirectoryPicker,
        IRunnerRemovalConfirmation runnerRemovalConfirmation)
        : this(
            configStore,
            runnerFactory,
            workingDirectoryPicker,
            runnerRemovalConfirmation,
            NullAppUpdateService.Instance)
    {
    }

    public MainWindowViewModel(
        RunnerConfigStore configStore,
        IRunnerFactory runnerFactory,
        IWorkingDirectoryPicker workingDirectoryPicker,
        IRunnerRemovalConfirmation runnerRemovalConfirmation,
        IAppUpdateService appUpdateService)
    {
        _configStore = configStore;
        _runnerFactory = runnerFactory;
        _workingDirectoryPicker = workingDirectoryPicker;
        _runnerRemovalConfirmation = runnerRemovalConfirmation;
        _appUpdateService = appUpdateService;
        Runners.CollectionChanged += OnRunnersChanged;
    }

    public ObservableCollection<RunnerViewModel> Runners { get; } = [];

    public event EventHandler? SettingsOpenRequested;

    public string ConfigPath => _configStore.FilePath;

    public WindowPlacement? WindowPlacement
    {
        get => _windowPlacement;
        private set => SetProperty(ref _windowPlacement, value);
    }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set
        {
            if (SetProperty(ref _alwaysOnTop, value))
            {
                OnPropertyChanged(nameof(AlwaysOnTopToolTip));
                OnPropertyChanged(nameof(AlwaysOnTopIconForeground));
                OnPropertyChanged(nameof(AlwaysOnTopButtonBackground));

                if (!_isLoading && !_suppressAlwaysOnTopSave)
                {
                    _ = SaveConfigAsync("Saved always-on-top preference.", saveRunnerChanges: false);
                }
            }
        }
    }

    public bool IsSettingsDirty
    {
        get => _isSettingsDirty;
        private set => SetProperty(ref _isSettingsDirty, value);
    }

    public RunnerViewModel? SelectedRunner
    {
        get => _selectedRunner;
        set
        {
            if (SetProperty(ref _selectedRunner, value))
            {
                OnPropertyChanged(nameof(HasSelectedRunner));
                RefreshCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? AvailableUpdateVersion
    {
        get => _availableUpdateVersion;
        private set
        {
            if (SetProperty(ref _availableUpdateVersion, value))
            {
                OnPropertyChanged(nameof(UpdateActionText));
                OnPropertyChanged(nameof(UpdateActionToolTip));
            }
        }
    }

    public int UpdateProgress
    {
        get => _updateProgress;
        private set
        {
            if (SetProperty(ref _updateProgress, value))
            {
                OnPropertyChanged(nameof(UpdateActionText));
            }
        }
    }

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set => SetProperty(ref _isCheckingForUpdates, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set
        {
            if (SetProperty(ref _isUpdateAvailable, value))
            {
                OnPropertyChanged(nameof(IsUpdateActionVisible));
                OnPropertyChanged(nameof(CanApplyUpdate));
                OnPropertyChanged(nameof(UpdateActionText));
                OnPropertyChanged(nameof(UpdateActionToolTip));
                ApplyUpdateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsUpdateDownloaded
    {
        get => _isUpdateDownloaded;
        private set
        {
            if (SetProperty(ref _isUpdateDownloaded, value))
            {
                OnPropertyChanged(nameof(UpdateActionText));
                OnPropertyChanged(nameof(UpdateActionToolTip));
            }
        }
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        private set
        {
            if (SetProperty(ref _isUpdating, value))
            {
                OnPropertyChanged(nameof(IsUpdateActionVisible));
                OnPropertyChanged(nameof(CanApplyUpdate));
                OnPropertyChanged(nameof(UpdateActionText));
                OnPropertyChanged(nameof(UpdateActionToolTip));
                ApplyUpdateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedRunner => SelectedRunner is not null;

    public bool HasNoRunners => Runners.Count == 0;

    public bool HasRunners => Runners.Count > 0;

    public bool CanStartSelected => SelectedRunner?.CanStart == true;

    public bool CanStopSelected => SelectedRunner?.CanStop == true;

    public bool CanRestartSelected => SelectedRunner?.CanRestart == true;

    public bool CanRunSelected => SelectedRunner?.CanRunPrimary == true;

    public bool CanCleanSelected => SelectedRunner?.CanClean == true;

    public bool CanBuildSelected => SelectedRunner?.CanBuild == true;

    public bool CanApplyUpdate => IsUpdateAvailable && !IsUpdating;

    public bool IsUpdateActionVisible => IsUpdateAvailable || IsUpdating;

    public double WindowMinWidth => 200;

    public double WindowMinHeight => 112;

    public string AlwaysOnTopToolTip => AlwaysOnTop ? "Disable always on top" : "Keep window on top";

    public string UpdateActionText
    {
        get
        {
            if (IsUpdating)
            {
                return UpdateProgress > 0
                    ? $"Updating {UpdateProgress}%"
                    : "Updating";
            }

            if (IsUpdateDownloaded)
            {
                return "Restart to update";
            }

            return string.IsNullOrWhiteSpace(AvailableUpdateVersion)
                ? "Update"
                : $"Update {AvailableUpdateVersion}";
        }
    }

    public string UpdateActionToolTip => IsUpdateDownloaded
        ? "Restart Runner to apply the downloaded update"
        : "Download, apply, and restart Runner";

    public IBrush AlwaysOnTopIconForeground => ToBrush(AlwaysOnTop ? "#2563EB" : "#667085");

    public IBrush AlwaysOnTopButtonBackground => ToBrush(AlwaysOnTop ? "#DBEAFE" : "#00000000");

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _isLoading = true;
            StatusMessage = "Loading configuration...";
            var config = await _configStore.LoadAsync(cancellationToken);

            AlwaysOnTop = config.AlwaysOnTop;
            SetLoadedWindowPlacement(config.WindowPlacement);
            Runners.Clear();

            foreach (var definition in config.Runners)
            {
                AddRunnerViewModel(definition);
            }

            CaptureSavedDefinitionsSnapshot();
            SelectedRunner = Runners.FirstOrDefault();
            StatusMessage = Runners.Count == 0
                ? "No runners configured. Add a runner to get started."
                : $"Loaded {Runners.Count} runner(s).";

            await CheckForUpdatesAsync(showNoUpdateStatus: false, cancellationToken);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load config: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    public async Task CheckForUpdatesAsync(
        bool showNoUpdateStatus = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IsCheckingForUpdates = true;
            var result = await _appUpdateService.CheckForUpdatesAsync(cancellationToken);

            AvailableUpdateVersion = result.AvailableVersion;
            IsUpdateDownloaded = result.IsDownloaded;
            IsUpdateAvailable = result.IsAvailable;

            if (result.IsAvailable || (showNoUpdateStatus && result.IsSupported))
            {
                StatusMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            IsUpdateAvailable = false;
            IsUpdateDownloaded = false;
            AvailableUpdateVersion = null;
            StatusMessage = $"Update check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var runner in Runners.ToArray())
        {
            if (runner.CanStop)
            {
                await runner.StopAsync(cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync();

        foreach (var runner in Runners.ToArray())
        {
            await runner.DisposeAsync();
        }
    }

    [RelayCommand]
    private async Task AddRunnerAsync()
    {
        var definition = new RunnerDefinition
        {
            DisplayName = "New .NET runner",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Command = "",
            Arguments = ""
        };

        var runner = AddRunnerViewModel(definition);
        SelectedRunner = runner;
        await SaveConfigAsync("Added runner.", saveRunnerChanges: true);
        RequestOpenSettings();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedRunner))]
    private async Task CloneSelectedAsync()
    {
        if (SelectedRunner is null)
        {
            return;
        }

        var source = SelectedRunner.Definition;
        var definition = new RunnerDefinition
        {
            DisplayName = $"{source.DisplayName} (Clone)",
            Type = source.Type,
            WorkingDirectory = source.WorkingDirectory,
            Command = source.Command,
            Arguments = source.Arguments,
            CleanBeforeRestore = source.CleanBeforeRestore,
            EnvironmentVariables = new Dictionary<string, string>(
                source.EnvironmentVariables,
                StringComparer.OrdinalIgnoreCase)
        };

        var selectedIndex = Runners.IndexOf(SelectedRunner);
        var insertIndex = selectedIndex < 0 ? Runners.Count : selectedIndex + 1;
        var runner = AddRunnerViewModel(definition, insertIndex);

        SelectedRunner = runner;
        await SaveConfigAsync("Cloned runner.", saveRunnerChanges: true);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedRunner))]
    private async Task RemoveSelectedAsync()
    {
        if (SelectedRunner is null)
        {
            return;
        }

        var runner = SelectedRunner;
        bool confirmed;

        try
        {
            confirmed = await _runnerRemovalConfirmation.ConfirmRemoveAsync(runner.Header);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Remove confirmation failed: {ex.Message}";
            return;
        }

        if (!confirmed)
        {
            StatusMessage = "Remove canceled.";
            return;
        }

        var nextSelectionIndex = Math.Max(0, Runners.IndexOf(runner) - 1);

        if (runner.CanStop)
        {
            await runner.StopAsync();
        }

        Runners.Remove(runner);
        await runner.DisposeAsync();
        SelectedRunner = Runners.Count == 0 ? null : Runners[Math.Min(nextSelectionIndex, Runners.Count - 1)];
        await SaveConfigAsync("Removed runner.", saveRunnerChanges: true);
    }

    [RelayCommand(CanExecute = nameof(CanMoveRunnerUp))]
    private void MoveRunnerUp(RunnerViewModel? runner)
    {
        if (runner is not null)
        {
            MoveRunner(runner, Runners.IndexOf(runner) - 1);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveRunnerDown))]
    private void MoveRunnerDown(RunnerViewModel? runner)
    {
        if (runner is not null)
        {
            MoveRunner(runner, Runners.IndexOf(runner) + 1);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartSelected))]
    private async Task StartSelectedAsync()
    {
        await StartRunnerAsync(SelectedRunner);
    }

    [RelayCommand(CanExecute = nameof(CanStopSelected))]
    private async Task StopSelectedAsync()
    {
        await StopRunnerAsync(SelectedRunner);
    }

    [RelayCommand(CanExecute = nameof(CanRestartSelected))]
    private async Task RestartSelectedAsync()
    {
        await RestartRunnerAsync(SelectedRunner);
    }

    [RelayCommand(CanExecute = nameof(CanRunSelected))]
    private async Task RunSelectedAsync()
    {
        await RunRunnerAsync(SelectedRunner);
    }

    [RelayCommand(CanExecute = nameof(CanCleanSelected))]
    private async Task CleanSelectedAsync()
    {
        await CleanRunnerAsync(SelectedRunner);
    }

    [RelayCommand(CanExecute = nameof(CanBuildSelected))]
    private async Task BuildSelectedAsync()
    {
        await BuildRunnerAsync(SelectedRunner);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedRunner))]
    private async Task BrowseWorkingDirectoryAsync()
    {
        if (SelectedRunner is null)
        {
            return;
        }

        try
        {
            var selectedPath = await _workingDirectoryPicker.PickWorkingDirectoryAsync(
                SelectedRunner.WorkingDirectory);

            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                StatusMessage = "Folder selection canceled.";
                return;
            }

            SelectedRunner.WorkingDirectory = selectedPath;
            StatusMessage = "Updated working directory.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Folder picker failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        await SaveConfigAsync("Saved configuration.", saveRunnerChanges: true);
    }

    [RelayCommand(CanExecute = nameof(CanApplyUpdate))]
    private async Task ApplyUpdateAsync()
    {
        try
        {
            IsUpdating = true;
            UpdateProgress = 0;
            StatusMessage = IsUpdateDownloaded
                ? "Restarting to apply update..."
                : "Downloading update...";

            var progress = new Progress<int>(value =>
            {
                UpdateProgress = value;
                StatusMessage = $"Downloading update... {value}%";
            });

            await _appUpdateService.ApplyUpdateAndRestartAsync(progress);
            StatusMessage = "Restarting to apply update...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update failed: {ex.Message}";
        }
        finally
        {
            IsUpdating = false;
        }
    }

    public Task SaveWindowPlacementAsync(
        WindowPlacement placement,
        CancellationToken cancellationToken = default)
    {
        WindowPlacement = placement;
        return SaveConfigAsync("Saved window placement.", saveRunnerChanges: false, cancellationToken);
    }

    private void SetLoadedWindowPlacement(WindowPlacement? placement)
    {
        if (!SetProperty(ref _windowPlacement, placement, nameof(WindowPlacement)))
        {
            OnPropertyChanged(nameof(WindowPlacement));
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        RequestOpenSettings();
    }

    [RelayCommand]
    private async Task ToggleAlwaysOnTopAsync()
    {
        _suppressAlwaysOnTopSave = true;

        try
        {
            AlwaysOnTop = !AlwaysOnTop;
        }
        finally
        {
            _suppressAlwaysOnTopSave = false;
        }

        if (!_isLoading)
        {
            await SaveConfigAsync("Saved always-on-top preference.", saveRunnerChanges: false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartRunner))]
    private async Task StartRunnerAsync(RunnerViewModel? runner)
    {
        if (runner is null)
        {
            return;
        }

        SelectedRunner = runner;
        var errors = RunnerDefinitionValidator.Validate(runner.Definition);

        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        try
        {
            StatusMessage = runner.IsBuildOnly
                ? $"Building {runner.Header}..."
                : $"Starting {runner.Header}...";
            await runner.StartAsync();
            StatusMessage = runner.IsBuildOnly
                ? $"Built {runner.Header}."
                : $"Started {runner.Header}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Start failed: {ex.Message}";
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopRunner))]
    private async Task StopRunnerAsync(RunnerViewModel? runner)
    {
        if (runner is null)
        {
            return;
        }

        SelectedRunner = runner;

        try
        {
            StatusMessage = $"Stopping {runner.Header}...";
            await runner.StopAsync();
            StatusMessage = $"Stopped {runner.Header}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Stop failed: {ex.Message}";
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestartRunner))]
    private async Task RestartRunnerAsync(RunnerViewModel? runner)
    {
        if (runner is null)
        {
            return;
        }

        SelectedRunner = runner;
        var errors = RunnerDefinitionValidator.Validate(runner.Definition);

        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        if (runner.IsBuildOnly)
        {
            await StartRunnerAsync(runner);
            return;
        }

        try
        {
            StatusMessage = $"Restarting {runner.Header}...";
            await runner.RestartAsync();
            StatusMessage = $"Restarted {runner.Header}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restart failed: {ex.Message}";
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCleanRunner))]
    private async Task CleanRunnerAsync(RunnerViewModel? runner)
    {
        if (runner is null)
        {
            return;
        }

        SelectedRunner = runner;
        var errors = RunnerDefinitionValidator.Validate(runner.Definition);

        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        try
        {
            StatusMessage = $"Cleaning {runner.Header}...";
            await runner.CleanAsync();
            StatusMessage = $"Cleaned {runner.Header}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clean failed: {ex.Message}";
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanBuildRunner))]
    private async Task BuildRunnerAsync(RunnerViewModel? runner)
    {
        if (runner is null)
        {
            return;
        }

        SelectedRunner = runner;
        var errors = RunnerDefinitionValidator.Validate(runner.Definition);

        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        try
        {
            StatusMessage = $"Building {runner.Header}...";
            await runner.BuildAsync();
            StatusMessage = $"Built {runner.Header}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Build failed: {ex.Message}";
        }
        finally
        {
            RefreshCommandStates();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunRunnerPrimary))]
    private async Task RunRunnerAsync(RunnerViewModel? runner)
    {
        if (runner is null)
        {
            return;
        }

        if (runner.CanStop)
        {
            await StopRunnerAsync(runner);
            return;
        }

        if (runner.IsBuildOnly)
        {
            await BuildRunnerAsync(runner);
            return;
        }

        await StartRunnerAsync(runner);
    }

    private RunnerViewModel AddRunnerViewModel(RunnerDefinition definition, int? insertIndex = null)
    {
        var runner = new RunnerViewModel(definition, _runnerFactory);
        runner.PropertyChanged += OnRunnerPropertyChanged;

        if (insertIndex is { } index)
        {
            Runners.Insert(index, runner);
        }
        else
        {
            Runners.Add(runner);
        }

        return runner;
    }

    public bool MoveRunner(RunnerViewModel runner, int targetIndex)
    {
        var currentIndex = Runners.IndexOf(runner);

        if (currentIndex < 0 || Runners.Count < 2)
        {
            return false;
        }

        var clampedTargetIndex = Math.Clamp(targetIndex, 0, Runners.Count - 1);

        if (currentIndex == clampedTargetIndex)
        {
            return false;
        }

        Runners.Move(currentIndex, clampedTargetIndex);
        SelectedRunner = runner;
        IsSettingsDirty = true;
        StatusMessage = "Reordered projects.";
        RefreshCommandStates();
        return true;
    }

    public void DiscardSettingsChanges()
    {
        _suppressSettingsDirtyTracking = true;

        try
        {
            foreach (var runner in Runners)
            {
                var savedDefinition = _lastSavedDefinitions.FirstOrDefault(
                    definition => string.Equals(definition.Id, runner.Id, StringComparison.Ordinal));

                if (savedDefinition is null)
                {
                    continue;
                }

                runner.DisplayName = savedDefinition.DisplayName;
                runner.Type = savedDefinition.Type;
                runner.WorkingDirectory = savedDefinition.WorkingDirectory;
                runner.Command = savedDefinition.Command;
                runner.Arguments = savedDefinition.Arguments;
                runner.CleanBeforeRestore = savedDefinition.CleanBeforeRestore;
                runner.EnvironmentVariablesText = FormatEnvironmentVariables(savedDefinition.EnvironmentVariables);
            }

            RestoreSavedRunnerOrder();
        }
        finally
        {
            _suppressSettingsDirtyTracking = false;
        }

        IsSettingsDirty = false;
        StatusMessage = "Discarded unsaved settings changes.";
    }

    private async Task SaveConfigAsync(
        string successMessage,
        bool saveRunnerChanges,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var runnerDefinitions = saveRunnerChanges || !IsSettingsDirty
                ? Runners.Select(runner => runner.Definition.Clone()).ToList()
                : _lastSavedDefinitions.Select(definition => definition.Clone()).ToList();

            var config = new RunnerConfig
            {
                AlwaysOnTop = AlwaysOnTop,
                WindowPlacement = WindowPlacement,
                Runners = runnerDefinitions
            };

            await _configStore.SaveAsync(config, cancellationToken);

            if (saveRunnerChanges)
            {
                CaptureSavedDefinitionsSnapshot();
                IsSettingsDirty = false;
            }

            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private void OnRunnersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoRunners));
        OnPropertyChanged(nameof(HasRunners));
        RefreshCommandStates();
    }

    private void OnRunnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RunnerViewModel.Status)
            or nameof(RunnerViewModel.ProcessId)
            or nameof(RunnerViewModel.Type)
            or nameof(RunnerViewModel.CanStart)
            or nameof(RunnerViewModel.CanStop)
            or nameof(RunnerViewModel.CanRestart)
            or nameof(RunnerViewModel.CanRunPrimary)
            or nameof(RunnerViewModel.CanClean)
            or nameof(RunnerViewModel.CanBuild))
        {
            RefreshCommandStates();
        }

        if (!_suppressSettingsDirtyTracking
            && e.PropertyName is nameof(RunnerViewModel.DisplayName)
                or nameof(RunnerViewModel.Type)
                or nameof(RunnerViewModel.WorkingDirectory)
                or nameof(RunnerViewModel.Command)
                or nameof(RunnerViewModel.Arguments)
                or nameof(RunnerViewModel.CleanBeforeRestore)
                or nameof(RunnerViewModel.EnvironmentVariablesText))
        {
            IsSettingsDirty = true;
        }
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanStartSelected));
        OnPropertyChanged(nameof(CanStopSelected));
        OnPropertyChanged(nameof(CanRestartSelected));
        OnPropertyChanged(nameof(CanRunSelected));
        OnPropertyChanged(nameof(CanCleanSelected));
        OnPropertyChanged(nameof(CanBuildSelected));

        AddRunnerCommand.NotifyCanExecuteChanged();
        CloneSelectedCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        MoveRunnerUpCommand.NotifyCanExecuteChanged();
        MoveRunnerDownCommand.NotifyCanExecuteChanged();
        BrowseWorkingDirectoryCommand.NotifyCanExecuteChanged();
        StartSelectedCommand.NotifyCanExecuteChanged();
        StopSelectedCommand.NotifyCanExecuteChanged();
        RestartSelectedCommand.NotifyCanExecuteChanged();
        RunSelectedCommand.NotifyCanExecuteChanged();
        CleanSelectedCommand.NotifyCanExecuteChanged();
        BuildSelectedCommand.NotifyCanExecuteChanged();
        StartRunnerCommand.NotifyCanExecuteChanged();
        StopRunnerCommand.NotifyCanExecuteChanged();
        RestartRunnerCommand.NotifyCanExecuteChanged();
        RunRunnerCommand.NotifyCanExecuteChanged();
        CleanRunnerCommand.NotifyCanExecuteChanged();
        BuildRunnerCommand.NotifyCanExecuteChanged();
    }

    private static bool CanStartRunner(RunnerViewModel? runner)
    {
        return runner?.CanStart == true;
    }

    private static bool CanStopRunner(RunnerViewModel? runner)
    {
        return runner?.CanStop == true;
    }

    private static bool CanRestartRunner(RunnerViewModel? runner)
    {
        return runner?.CanRestart == true;
    }

    private static bool CanRunRunnerPrimary(RunnerViewModel? runner)
    {
        return runner?.CanRunPrimary == true;
    }

    private static bool CanCleanRunner(RunnerViewModel? runner)
    {
        return runner?.CanClean == true;
    }

    private static bool CanBuildRunner(RunnerViewModel? runner)
    {
        return runner?.CanBuild == true;
    }

    private bool CanMoveRunnerUp(RunnerViewModel? runner)
    {
        return runner is not null && Runners.IndexOf(runner) > 0;
    }

    private bool CanMoveRunnerDown(RunnerViewModel? runner)
    {
        return runner is not null
            && Runners.IndexOf(runner) is var index
            && index >= 0
            && index < Runners.Count - 1;
    }

    private void RequestOpenSettings()
    {
        SettingsOpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CaptureSavedDefinitionsSnapshot()
    {
        _lastSavedDefinitions = Runners.Select(runner => runner.Definition.Clone()).ToList();
    }

    private void RestoreSavedRunnerOrder()
    {
        for (var savedIndex = 0; savedIndex < _lastSavedDefinitions.Count; savedIndex++)
        {
            var savedDefinition = _lastSavedDefinitions[savedIndex];
            var currentIndex = Runners
                .Select((runner, index) => new { runner, index })
                .FirstOrDefault(item => string.Equals(
                    item.runner.Id,
                    savedDefinition.Id,
                    StringComparison.Ordinal))
                ?.index;

            if (currentIndex is { } index && index != savedIndex)
            {
                Runners.Move(index, savedIndex);
            }
        }
    }

    private static string FormatEnvironmentVariables(IReadOnlyDictionary<string, string> environmentVariables)
    {
        return string.Join(
            Environment.NewLine,
            environmentVariables.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static IBrush ToBrush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }
}
