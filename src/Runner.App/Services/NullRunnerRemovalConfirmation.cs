namespace Runner.App.Services;

public sealed class NullRunnerRemovalConfirmation : IRunnerRemovalConfirmation
{
    public static NullRunnerRemovalConfirmation Instance { get; } = new();

    private NullRunnerRemovalConfirmation()
    {
    }

    public Task<bool> ConfirmRemoveAsync(string runnerName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
