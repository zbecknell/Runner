using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace Runner.App.ViewModels;

public sealed partial class RunnerDetailsViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _refreshTimer;
    private RunnerViewModel _runner;

    public RunnerDetailsViewModel(MainWindowViewModel dashboard, RunnerViewModel runner)
    {
        Dashboard = dashboard;
        _runner = runner;
        Dashboard.PropertyChanged += OnDashboardPropertyChanged;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _refreshTimer.Start();
        RefreshLogs();
    }

    public MainWindowViewModel Dashboard { get; }

    public RunnerViewModel Runner
    {
        get => _runner;
        set
        {
            if (SetProperty(ref _runner, value))
            {
                OnPropertyChanged(nameof(LogText));
                OnPropertyChanged(nameof(HasLogs));
                OnPropertyChanged(nameof(HasNoLogs));
                RefreshLogs();
            }
        }
    }

    public string LogText => FormatLogLines(Runner.LogLines, Dashboard.ShowNewestLogsFirst);

    public bool HasLogs => Runner.HasLogs;

    public bool HasNoLogs => !HasLogs;

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        Dashboard.PropertyChanged -= OnDashboardPropertyChanged;
    }

    [RelayCommand]
    private void RefreshLogs()
    {
        Runner.RefreshLogState();
        OnPropertyChanged(nameof(LogText));
        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(HasNoLogs));
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Runner.ClearLogsCommand.Execute(null);
        RefreshLogs();
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshLogs();
    }

    private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ShowNewestLogsFirst))
        {
            RefreshLogs();
        }
    }

    private static string FormatLogLines(IReadOnlyList<string> logLines, bool newestFirst)
    {
        if (logLines.Count == 0)
        {
            return "";
        }

        var orderedLines = newestFirst
            ? logLines.Reverse()
            : logLines;

        return string.Join(Environment.NewLine, orderedLines);
    }
}
