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
    private bool _alwaysOnTop;
    private bool _isEditMode;
    private bool _isLoading;
    private bool _suppressAlwaysOnTopSave;
    private RunnerViewModel? _selectedRunner;
    private string _statusMessage = "Loading configuration...";

    public MainWindowViewModel()
        : this(
            new RunnerConfigStore(RunnerConfigStore.GetDefaultConfigPath()),
            new RunnerFactory(),
            NullWorkingDirectoryPicker.Instance,
            NullRunnerRemovalConfirmation.Instance)
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
            NullRunnerRemovalConfirmation.Instance)
    {
    }

    public MainWindowViewModel(
        RunnerConfigStore configStore,
        IRunnerFactory runnerFactory,
        IWorkingDirectoryPicker workingDirectoryPicker,
        IRunnerRemovalConfirmation runnerRemovalConfirmation)
    {
        _configStore = configStore;
        _runnerFactory = runnerFactory;
        _workingDirectoryPicker = workingDirectoryPicker;
        _runnerRemovalConfirmation = runnerRemovalConfirmation;
        Runners.CollectionChanged += OnRunnersChanged;
    }

    public ObservableCollection<RunnerViewModel> Runners { get; } = [];

    public string ConfigPath => _configStore.FilePath;

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
                    _ = SaveConfigAsync("Saved always-on-top preference.");
                }
            }
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(IsDashboardMode));
                OnPropertyChanged(nameof(EditModeButtonText));
                OnPropertyChanged(nameof(EditModeIconValue));
            }
        }
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

    public bool IsDashboardMode => !IsEditMode;

    public bool HasSelectedRunner => SelectedRunner is not null;

    public bool HasNoRunners => Runners.Count == 0;

    public bool HasRunners => Runners.Count > 0;

    public bool CanStartSelected => SelectedRunner?.CanStart == true;

    public bool CanStopSelected => SelectedRunner?.CanStop == true;

    public bool CanRestartSelected => SelectedRunner?.CanRestart == true;

    public bool CanRunSelected => SelectedRunner?.CanRunPrimary == true;

    public string EditModeButtonText => IsEditMode ? "Done" : "Edit";

    public string EditModeIconValue => IsEditMode ? "fa-solid fa-check" : "fa-solid fa-pen";

    public string AlwaysOnTopToolTip => AlwaysOnTop ? "Disable always on top" : "Keep window on top";

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
            Runners.Clear();

            foreach (var definition in config.Runners)
            {
                AddRunnerViewModel(definition);
            }

            SelectedRunner = Runners.FirstOrDefault();
            StatusMessage = Runners.Count == 0
                ? "No runners configured. Add a runner to get started."
                : $"Loaded {Runners.Count} runner(s).";
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
        IsEditMode = true;
        await SaveConfigAsync("Added runner.");
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
            EnvironmentVariables = new Dictionary<string, string>(
                source.EnvironmentVariables,
                StringComparer.OrdinalIgnoreCase)
        };

        var selectedIndex = Runners.IndexOf(SelectedRunner);
        var insertIndex = selectedIndex < 0 ? Runners.Count : selectedIndex + 1;
        var runner = AddRunnerViewModel(definition, insertIndex);

        SelectedRunner = runner;
        IsEditMode = true;
        await SaveConfigAsync("Cloned runner.");
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
        await SaveConfigAsync("Removed runner.");
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
            await SaveConfigAsync("Updated working directory.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Folder picker failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        await SaveConfigAsync("Saved configuration.");
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
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
            await SaveConfigAsync("Saved always-on-top preference.");
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
            StatusMessage = $"Starting {runner.Header}...";
            await runner.StartAsync();
            StatusMessage = $"Started {runner.Header}.";
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

    [RelayCommand(CanExecute = nameof(CanRunRunnerPrimary))]
    private async Task RunRunnerAsync(RunnerViewModel? runner)
    {
        if (runner is null)
        {
            return;
        }

        if (runner.Status == RunnerStatus.Running)
        {
            await RestartRunnerAsync(runner);
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

    private async Task SaveConfigAsync(string successMessage)
    {
        try
        {
            var config = new RunnerConfig
            {
                AlwaysOnTop = AlwaysOnTop,
                Runners = Runners.Select(runner => runner.Definition.Clone()).ToList()
            };

            await _configStore.SaveAsync(config);
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
            or nameof(RunnerViewModel.CanStart)
            or nameof(RunnerViewModel.CanStop)
            or nameof(RunnerViewModel.CanRestart)
            or nameof(RunnerViewModel.CanRunPrimary))
        {
            RefreshCommandStates();
        }
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(CanStartSelected));
        OnPropertyChanged(nameof(CanStopSelected));
        OnPropertyChanged(nameof(CanRestartSelected));
        OnPropertyChanged(nameof(CanRunSelected));

        AddRunnerCommand.NotifyCanExecuteChanged();
        CloneSelectedCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        BrowseWorkingDirectoryCommand.NotifyCanExecuteChanged();
        StartSelectedCommand.NotifyCanExecuteChanged();
        StopSelectedCommand.NotifyCanExecuteChanged();
        RestartSelectedCommand.NotifyCanExecuteChanged();
        RunSelectedCommand.NotifyCanExecuteChanged();
        StartRunnerCommand.NotifyCanExecuteChanged();
        StopRunnerCommand.NotifyCanExecuteChanged();
        RestartRunnerCommand.NotifyCanExecuteChanged();
        RunRunnerCommand.NotifyCanExecuteChanged();
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

    private static IBrush ToBrush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }
}
