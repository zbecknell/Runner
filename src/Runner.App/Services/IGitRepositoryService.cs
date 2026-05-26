namespace Runner.App.Services;

public interface IGitRepositoryService
{
    Task<GitRepositoryInfo> GetRepositoryInfoAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
