namespace Runner.Core.Runners;

public sealed class RunnerDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string DisplayName { get; set; } = "";

    public RunnerType Type { get; set; } = RunnerType.DotNetProject;

    public string WorkingDirectory { get; set; } = "";

    public string Command { get; set; } = "";

    public string Arguments { get; set; } = "";

    public bool CleanBeforeRestore { get; set; }

    public RunnerCommandSet CustomCommands { get; set; } = new();

    public Dictionary<string, string> EnvironmentVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = Guid.NewGuid().ToString("N");
        }
    }

    public RunnerDefinition Clone()
    {
        return new RunnerDefinition
        {
            Id = Id,
            DisplayName = DisplayName,
            Type = Type,
            WorkingDirectory = WorkingDirectory,
            Command = Command,
            Arguments = Arguments,
            CleanBeforeRestore = CleanBeforeRestore,
            CustomCommands = CustomCommands?.Clone() ?? new RunnerCommandSet(),
            EnvironmentVariables = new Dictionary<string, string>(EnvironmentVariables, StringComparer.OrdinalIgnoreCase)
        };
    }
}
