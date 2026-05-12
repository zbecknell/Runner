namespace Runner.App.Services;

public interface IRunnerRemovalConfirmation
{
    Task<bool> ConfirmRemoveAsync(string runnerName, CancellationToken cancellationToken = default);
}
