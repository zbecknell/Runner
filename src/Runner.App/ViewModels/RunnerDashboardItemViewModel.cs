namespace Runner.App.ViewModels;

public sealed class RunnerDashboardItemViewModel(RunnerViewModel runner) : DashboardItemViewModel
{
    public RunnerViewModel Runner { get; } = runner;
}
