using Runner.Core.Runners;

namespace Runner.Core.Config;

public sealed class RunnerConfig
{
    public bool AlwaysOnTop { get; set; }

    public List<RunnerDefinition> Runners { get; set; } = [];
}
