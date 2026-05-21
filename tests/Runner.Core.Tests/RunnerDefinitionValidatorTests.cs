using Runner.Core.Runners;

namespace Runner.Core.Tests;

public sealed class RunnerDefinitionValidatorTests
{
    [Fact]
    public void Validate_RequiresDisplayNameAndWorkingDirectory()
    {
        var errors = RunnerDefinitionValidator.Validate(new RunnerDefinition());

        Assert.Contains("Display name is required.", errors);
        Assert.Contains("Working directory is required.", errors);
    }

    [Fact]
    public void Validate_AcceptsExistingDotNetProjectDirectory()
    {
        using var directory = TempDirectory.Create();

        var errors = RunnerDefinitionValidator.Validate(new RunnerDefinition
        {
            DisplayName = "Worker",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = directory.Path
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AcceptsExistingDotNetProjectBuildDirectory()
    {
        using var directory = TempDirectory.Create();

        var errors = RunnerDefinitionValidator.Validate(new RunnerDefinition
        {
            DisplayName = "Worker build",
            Type = RunnerType.DotNetProjectBuild,
            WorkingDirectory = directory.Path
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RejectsMissingProjectPath()
    {
        using var directory = TempDirectory.Create();

        var errors = RunnerDefinitionValidator.Validate(new RunnerDefinition
        {
            DisplayName = "Worker",
            Type = RunnerType.DotNetProject,
            WorkingDirectory = directory.Path,
            Command = "Missing.csproj"
        });

        Assert.Contains(errors, error => error.Contains("Project path does not exist", StringComparison.Ordinal));
    }
}
