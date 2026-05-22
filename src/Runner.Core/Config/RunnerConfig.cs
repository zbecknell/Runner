using Runner.Core.Runners;

namespace Runner.Core.Config;

public sealed class RunnerConfig
{
    public bool AlwaysOnTop { get; set; }

    public bool ShowNewestLogsFirst { get; set; } = true;

    public bool ShowProjectPaths { get; set; } = true;

    public double RunnerOpacity { get; set; } = 1.0;

    public WindowPlacement? WindowPlacement { get; set; }

    public WindowPlacement? SettingsWindowPlacement { get; set; }

    public WindowPlacement? DetailsWindowPlacement { get; set; }

    public List<RunnerDefinition> Runners { get; set; } = [];
}
