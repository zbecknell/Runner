namespace Runner.App.Services;

public enum GitRepositoryState
{
    Repository,
    NoRepository,
    Unknown
}

public sealed record GitRepositoryInfo(
    GitRepositoryState State,
    string? RepositoryRoot,
    string RepositoryName,
    string BranchDisplay,
    string? ErrorMessage)
{
    public bool HasRepository => State == GitRepositoryState.Repository;

    public static GitRepositoryInfo Repository(
        string repositoryRoot,
        string repositoryName,
        string branchDisplay,
        string? errorMessage = null)
    {
        return new GitRepositoryInfo(
            GitRepositoryState.Repository,
            repositoryRoot,
            repositoryName,
            branchDisplay,
            errorMessage);
    }

    public static GitRepositoryInfo NoRepository()
    {
        return new GitRepositoryInfo(
            GitRepositoryState.NoRepository,
            null,
            "No Git repository",
            "Not in Git",
            null);
    }

    public static GitRepositoryInfo Unknown(string? errorMessage)
    {
        return new GitRepositoryInfo(
            GitRepositoryState.Unknown,
            null,
            "Git unavailable",
            "Branch unknown",
            errorMessage);
    }
}
