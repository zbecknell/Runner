using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Optris.Icons.Avalonia;
using Runner.Core.Runners;

namespace Runner.App.ViewModels;

public sealed partial class RunnerViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IRunner _runner;
    private string _displayName;
    private string _workingDirectory;
    private string _command;
    private string _arguments;
    private string _environmentVariablesText;
    private RunnerStatus _status;
    private int? _processId;
    private bool _isFailureDetailsVisible;

    public RunnerViewModel(RunnerDefinition definition, IRunnerFactory runnerFactory)
    {
        Definition = definition;
        Definition.EnsureId();

        _displayName = definition.DisplayName;
        _workingDirectory = definition.WorkingDirectory;
        _command = definition.Command;
        _arguments = definition.Arguments;
        _environmentVariablesText = FormatEnvironmentVariables(definition.EnvironmentVariables);
        _runner = runnerFactory.Create(definition);
        _status = _runner.Status;
        _processId = _runner.ProcessId;

        _runner.StatusChanged += OnStatusChanged;
    }

    public RunnerDefinition Definition { get; }

    public string Id => Definition.Id;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                Definition.DisplayName = value;
                OnPropertyChanged(nameof(Header));
            }
        }
    }

    public RunnerType Type
    {
        get => Definition.Type;
        set
        {
            if (Definition.Type == value)
            {
                return;
            }

            Definition.Type = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Header));
        }
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (SetProperty(ref _workingDirectory, value))
            {
                Definition.WorkingDirectory = value;
            }
        }
    }

    public string Command
    {
        get => _command;
        set
        {
            if (SetProperty(ref _command, value))
            {
                Definition.Command = value;
            }
        }
    }

    public string Arguments
    {
        get => _arguments;
        set
        {
            if (SetProperty(ref _arguments, value))
            {
                Definition.Arguments = value;
            }
        }
    }

    public string EnvironmentVariablesText
    {
        get => _environmentVariablesText;
        set
        {
            if (SetProperty(ref _environmentVariablesText, value))
            {
                Definition.EnvironmentVariables = ParseEnvironmentVariables(value);
            }
        }
    }

    public RunnerStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(Header));
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(StatusForeground));
                OnPropertyChanged(nameof(StatusBorderBrush));
                OnPropertyChanged(nameof(StatusBackgroundColor));
                OnPropertyChanged(nameof(StatusForegroundColor));
                OnPropertyChanged(nameof(StatusBorderColor));
                OnPropertyChanged(nameof(StatusIconValue));
                OnPropertyChanged(nameof(StatusIconAnimation));
                OnPropertyChanged(nameof(PrimaryRunText));
                OnPropertyChanged(nameof(PrimaryRunIconValue));
                OnPropertyChanged(nameof(PrimaryRunIconAnimation));
                OnPropertyChanged(nameof(PrimaryRunToolTip));
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
                OnPropertyChanged(nameof(CanRestart));
                OnPropertyChanged(nameof(CanRunPrimary));
            }
        }
    }

    public int? ProcessId
    {
        get => _processId;
        private set
        {
            if (SetProperty(ref _processId, value))
            {
                OnPropertyChanged(nameof(ProcessText));
            }
        }
    }

    public string StatusText => Status.ToString();

    public string StatusBackgroundColor => Status switch
    {
        RunnerStatus.Running => "#166534",
        RunnerStatus.Failed => "#991B1B",
        RunnerStatus.Restoring or RunnerStatus.Building or RunnerStatus.Starting => "#1D4ED8",
        RunnerStatus.Stopping => "#92400E",
        _ => "#334155"
    };

    public string StatusForegroundColor => Status switch
    {
        RunnerStatus.Running => "#ECFDF5",
        RunnerStatus.Failed => "#FEF2F2",
        RunnerStatus.Restoring or RunnerStatus.Building or RunnerStatus.Starting => "#EFF6FF",
        RunnerStatus.Stopping => "#FFFBEB",
        _ => "#F8FAFC"
    };

    public string StatusBorderColor => Status switch
    {
        RunnerStatus.Running => "#22C55E",
        RunnerStatus.Failed => "#EF4444",
        RunnerStatus.Restoring or RunnerStatus.Building or RunnerStatus.Starting => "#60A5FA",
        RunnerStatus.Stopping => "#F59E0B",
        _ => "#64748B"
    };

    public string StatusIconValue => Status switch
    {
        RunnerStatus.Running => "fa-solid fa-circle-play",
        RunnerStatus.Failed => "fa-solid fa-triangle-exclamation",
        RunnerStatus.Restoring or RunnerStatus.Building or RunnerStatus.Starting => "fa-solid fa-spinner",
        RunnerStatus.Stopping => "fa-solid fa-circle-stop",
        _ => "fa-solid fa-circle"
    };

    public IconAnimation StatusIconAnimation => IsSpinnerStatus
        ? IconAnimation.Spin
        : IconAnimation.None;

    public string PrimaryRunText => Status switch
    {
        RunnerStatus.Running => "Restart",
        RunnerStatus.Restoring => "Restoring",
        RunnerStatus.Building => "Building",
        RunnerStatus.Starting => "Starting",
        _ => "Start"
    };

    public string PrimaryRunIconValue => Status switch
    {
        RunnerStatus.Running => "fa-solid fa-rotate-right",
        RunnerStatus.Restoring or RunnerStatus.Building or RunnerStatus.Starting => "fa-solid fa-spinner",
        _ => "fa-solid fa-play"
    };

    public IconAnimation PrimaryRunIconAnimation => IsSpinnerStatus
        ? IconAnimation.Spin
        : IconAnimation.None;

    public string PrimaryRunToolTip => Status switch
    {
        RunnerStatus.Running => "Restart",
        RunnerStatus.Restoring => "Restoring",
        RunnerStatus.Building => "Building",
        RunnerStatus.Starting => "Starting",
        _ => "Start"
    };

    public IBrush StatusBackground => ToBrush(StatusBackgroundColor);

    public IBrush StatusForeground => ToBrush(StatusForegroundColor);

    public IBrush StatusBorderBrush => ToBrush(StatusBorderColor);

    public string ProcessText => ProcessId is { } processId ? $"PID {processId}" : "No process";

    public string Header => string.IsNullOrWhiteSpace(DisplayName)
        ? $"{Type} runner"
        : DisplayName;

    public bool CanStart => Status is RunnerStatus.Stopped or RunnerStatus.Failed;

    public bool CanStop => Status is RunnerStatus.Restoring
        or RunnerStatus.Building
        or RunnerStatus.Starting
        or RunnerStatus.Running;

    public bool CanRestart => Status is RunnerStatus.Running or RunnerStatus.Failed or RunnerStatus.Stopped;

    public bool CanRunPrimary => Status is RunnerStatus.Running or RunnerStatus.Failed or RunnerStatus.Stopped;

    private bool IsSpinnerStatus => Status is RunnerStatus.Restoring or RunnerStatus.Building or RunnerStatus.Starting;

    public bool HasFailureDetails => _runner.LastFailure is not null;

    public bool IsFailureDetailsVisible
    {
        get => _isFailureDetailsVisible;
        set => SetProperty(ref _isFailureDetailsVisible, value && HasFailureDetails);
    }

    public string FailureDetailsText => FormatFailureDetails(_runner.LastFailure);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _runner.StartAsync(cancellationToken);
        }
        finally
        {
            RefreshProcessState();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _runner.StopAsync(cancellationToken);
        }
        finally
        {
            RefreshProcessState();
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _runner.RestartAsync(cancellationToken);
        }
        finally
        {
            RefreshProcessState();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _runner.StatusChanged -= OnStatusChanged;
        await _runner.DisposeAsync();
    }

    [RelayCommand(CanExecute = nameof(HasFailureDetails))]
    private void ShowFailureDetails()
    {
        IsFailureDetailsVisible = true;
    }

    [RelayCommand]
    private void HideFailureDetails()
    {
        IsFailureDetailsVisible = false;
    }

    private static IBrush ToBrush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }

    private static string FormatEnvironmentVariables(IReadOnlyDictionary<string, string> environmentVariables)
    {
        return string.Join(
            Environment.NewLine,
            environmentVariables.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static Dictionary<string, string> ParseEnvironmentVariables(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in value.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');

            if (separatorIndex < 0)
            {
                result[line] = "";
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var variableValue = line[(separatorIndex + 1)..];
            result[key] = variableValue;
        }

        return result;
    }

    private static string FormatFailureDetails(RunnerFailureDetails? failure)
    {
        if (failure is null)
        {
            return "";
        }

        var lines = new List<string>
        {
            $"Time: {failure.Timestamp:yyyy-MM-dd HH:mm:ss zzz}",
            $"Reason: {failure.Reason}"
        };

        if (failure.ExitCode is { } exitCode)
        {
            lines.Add($"Exit code: {exitCode}");
        }

        if (!string.IsNullOrWhiteSpace(failure.ExceptionMessage))
        {
            lines.Add($"Exception: {failure.ExceptionMessage}");
        }

        lines.Add("");
        lines.Add("Diagnostics:");

        if (failure.DiagnosticLines.Count == 0)
        {
            lines.Add("No process output was captured.");
        }
        else
        {
            lines.AddRange(failure.DiagnosticLines);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void OnStatusChanged(object? sender, RunnerStatus status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Status = status;
            RefreshProcessState();
        });
    }

    private void RefreshProcessState()
    {
        Status = _runner.Status;
        ProcessId = _runner.ProcessId;
        RefreshFailureState();
    }

    private void RefreshFailureState()
    {
        if (!HasFailureDetails && IsFailureDetailsVisible)
        {
            IsFailureDetailsVisible = false;
        }

        OnPropertyChanged(nameof(HasFailureDetails));
        OnPropertyChanged(nameof(FailureDetailsText));
        OnPropertyChanged(nameof(IsFailureDetailsVisible));
        ShowFailureDetailsCommand.NotifyCanExecuteChanged();
    }
}
