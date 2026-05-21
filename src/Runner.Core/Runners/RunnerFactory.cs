namespace Runner.Core.Runners;

public interface IRunnerFactory
{
    IRunner Create(RunnerDefinition definition);
}

public sealed class RunnerFactory : IRunnerFactory
{
    public IRunner Create(RunnerDefinition definition)
    {
        return definition.Type switch
        {
            RunnerType.DotNetProject => new DotNetProjectRunner(definition),
            RunnerType.DotNetProjectBuild => new DotNetProjectRunner(definition),
            _ => throw new NotSupportedException($"Runner type '{definition.Type}' is not supported yet.")
        };
    }
}
