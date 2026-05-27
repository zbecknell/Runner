namespace Runner.Core.Runners;

public static class RunnerDefinitionValidator
{
    public static IReadOnlyList<string> Validate(RunnerDefinition definition)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            errors.Add("Display name is required.");
        }

        if (definition.Type is not (RunnerType.DotNetProject or RunnerType.DotNetProjectBuild or RunnerType.CustomCommands))
        {
            errors.Add($"Runner type '{definition.Type}' is not supported yet.");
        }

        if (string.IsNullOrWhiteSpace(definition.WorkingDirectory))
        {
            errors.Add("Working directory is required.");
        }
        else if (!Directory.Exists(definition.WorkingDirectory))
        {
            errors.Add($"Working directory does not exist: {definition.WorkingDirectory}");
        }

        if (definition.Type is (RunnerType.DotNetProject or RunnerType.DotNetProjectBuild)
            && !string.IsNullOrWhiteSpace(definition.Command))
        {
            var projectPath = Path.IsPathRooted(definition.Command)
                ? definition.Command
                : Path.Combine(definition.WorkingDirectory, definition.Command);

            if (!File.Exists(projectPath) && !Directory.Exists(projectPath))
            {
                errors.Add($"Project path does not exist: {projectPath}");
            }
        }

        foreach (var key in definition.EnvironmentVariables.Keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add("Environment variable names cannot be blank.");
            }
            else if (key.Contains('='))
            {
                errors.Add($"Environment variable name cannot contain '=': {key}");
            }
        }

        return errors;
    }

    public static void ThrowIfInvalid(RunnerDefinition definition)
    {
        var errors = Validate(definition);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }
}
