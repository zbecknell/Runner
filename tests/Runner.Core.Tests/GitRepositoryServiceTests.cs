using Runner.App.Services;

namespace Runner.Core.Tests;

public sealed class GitRepositoryServiceTests
{
    [Fact]
    public async Task GetRepositoryInfoAsync_FindsParentGitDirectory()
    {
        using var directory = TempDirectory.Create();
        var repositoryRoot = Path.Combine(directory.Path, "repo");
        var projectDirectory = Path.Combine(repositoryRoot, "src", "App");
        Directory.CreateDirectory(Path.Combine(repositoryRoot, ".git"));
        Directory.CreateDirectory(projectDirectory);
        var service = new GitRepositoryService();

        var info = await service.GetRepositoryInfoAsync(projectDirectory);

        Assert.Equal(GitRepositoryState.Repository, info.State);
        Assert.Equal(repositoryRoot, info.RepositoryRoot);
        Assert.Equal("repo", info.RepositoryName);
    }

    [Fact]
    public async Task GetRepositoryInfoAsync_AcceptsGitFile()
    {
        using var directory = TempDirectory.Create();
        var repositoryRoot = Path.Combine(directory.Path, "worktree");
        var projectDirectory = Path.Combine(repositoryRoot, "src");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(repositoryRoot, ".git"),
            "gitdir: ../repo/.git/worktrees/worktree");
        var service = new GitRepositoryService();

        var info = await service.GetRepositoryInfoAsync(projectDirectory);

        Assert.Equal(GitRepositoryState.Repository, info.State);
        Assert.Equal(repositoryRoot, info.RepositoryRoot);
        Assert.Equal("worktree", info.RepositoryName);
    }

    [Fact]
    public async Task GetRepositoryInfoAsync_ReturnsNoRepositoryForExistingNonRepositoryDirectory()
    {
        using var directory = TempDirectory.Create();
        var projectDirectory = Path.Combine(directory.Path, "src");
        Directory.CreateDirectory(projectDirectory);
        var service = new GitRepositoryService();

        var info = await service.GetRepositoryInfoAsync(projectDirectory);

        Assert.Equal(GitRepositoryState.NoRepository, info.State);
        Assert.Null(info.RepositoryRoot);
    }

    [Fact]
    public async Task GetRepositoryInfoAsync_ReturnsNoRepositoryForMissingDirectory()
    {
        using var directory = TempDirectory.Create();
        var service = new GitRepositoryService();

        var info = await service.GetRepositoryInfoAsync(Path.Combine(directory.Path, "missing"));

        Assert.Equal(GitRepositoryState.NoRepository, info.State);
        Assert.Null(info.RepositoryRoot);
    }
}
