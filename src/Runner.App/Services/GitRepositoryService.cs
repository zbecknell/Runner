using System.ComponentModel;
using System.Diagnostics;

namespace Runner.App.Services;

public sealed class GitRepositoryService : IGitRepositoryService
{
    private static readonly TimeSpan GitCommandTimeout = TimeSpan.FromSeconds(2);

    public async Task<GitRepositoryInfo> GetRepositoryInfoAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            return GitRepositoryInfo.NoRepository();
        }

        var repositoryRoot = FindRepositoryRoot(workingDirectory);

        if (repositoryRoot is null)
        {
            return GitRepositoryInfo.NoRepository();
        }

        var repositoryName = Path.GetFileName(repositoryRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(repositoryName))
        {
            repositoryName = repositoryRoot;
        }

        var branchResult = await RunGitAsync(
            repositoryRoot,
            ["branch", "--show-current"],
            cancellationToken);

        if (branchResult.IsSuccess && !string.IsNullOrWhiteSpace(branchResult.Output))
        {
            return GitRepositoryInfo.Repository(
                repositoryRoot,
                repositoryName,
                branchResult.Output.Trim());
        }

        var commitResult = await RunGitAsync(
            repositoryRoot,
            ["rev-parse", "--short", "HEAD"],
            cancellationToken);

        if (commitResult.IsSuccess && !string.IsNullOrWhiteSpace(commitResult.Output))
        {
            return GitRepositoryInfo.Repository(
                repositoryRoot,
                repositoryName,
                commitResult.Output.Trim());
        }

        return GitRepositoryInfo.Repository(
            repositoryRoot,
            repositoryName,
            "Branch unknown",
            branchResult.ErrorMessage ?? commitResult.ErrorMessage);
    }

    private static string? FindRepositoryRoot(string workingDirectory)
    {
        for (var directory = new DirectoryInfo(workingDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");

            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    private static async Task<GitCommandResult> RunGitAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(GitCommandTimeout);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("-C");
        process.StartInfo.ArgumentList.Add(repositoryRoot);

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            if (!process.Start())
            {
                return GitCommandResult.Failed("Git did not start.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);

            await process.WaitForExitAsync(timeout.Token);

            var output = await outputTask;
            var error = await errorTask;

            return process.ExitCode == 0
                ? GitCommandResult.Success(output)
                : GitCommandResult.Failed(string.IsNullOrWhiteSpace(error)
                    ? $"Git exited with code {process.ExitCode}."
                    : error.Trim());
        }
        catch (Win32Exception ex)
        {
            return GitCommandResult.Failed(ex.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return GitCommandResult.Failed("Git timed out.");
        }
        catch (InvalidOperationException ex)
        {
            return GitCommandResult.Failed(ex.Message);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record GitCommandResult(bool IsSuccess, string Output, string? ErrorMessage)
    {
        public static GitCommandResult Success(string output)
        {
            return new GitCommandResult(true, output, null);
        }

        public static GitCommandResult Failed(string errorMessage)
        {
            return new GitCommandResult(false, "", errorMessage);
        }
    }
}
