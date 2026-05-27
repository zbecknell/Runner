namespace Runner.Core.Runners;

public sealed class RunnerCommandSet
{
    public string Clean { get; set; } = "";

    public string Restore { get; set; } = "";

    public string Build { get; set; } = "";

    public string Run { get; set; } = "";

    public RunnerCommandSet Clone()
    {
        return new RunnerCommandSet
        {
            Clean = Clean,
            Restore = Restore,
            Build = Build,
            Run = Run
        };
    }
}
