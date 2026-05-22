namespace Runner.Core.Runners;

public interface IRunner : IAsyncDisposable
{
    event EventHandler<RunnerStatus>? StatusChanged;

    RunnerDefinition Definition { get; }

    RunnerStatus Status { get; }

    int? ProcessId { get; }

    RunnerFailureDetails? LastFailure { get; }

    IReadOnlyList<string> LogLines { get; }

    void ClearLogs();

    Task CleanAsync(CancellationToken cancellationToken = default);

    Task BuildAsync(CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task RestartAsync(CancellationToken cancellationToken = default);
}
