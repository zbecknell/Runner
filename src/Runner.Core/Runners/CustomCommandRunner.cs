using System.Diagnostics;

namespace Runner.Core.Runners;

public sealed class CustomCommandRunner : IRunner
{
    private const int MaxDiagnosticLines = 100;
    private const int MaxLogLines = 2000;

    private readonly Lock _sync = new();
    private readonly List<string> _logLines = [];
    private Process? _process;
    private RunnerFailureDetails? _lastFailure;
    private RunnerStatus _status = RunnerStatus.Stopped;

    public CustomCommandRunner(RunnerDefinition definition)
    {
        Definition = definition;
        Definition.EnsureId();
        Definition.CustomCommands ??= new RunnerCommandSet();
    }

    public event EventHandler<RunnerStatus>? StatusChanged;

    public RunnerDefinition Definition { get; }

    public RunnerStatus Status
    {
        get
        {
            lock (_sync)
            {
                return _status;
            }
        }
    }

    public int? ProcessId => null;

    public RunnerFailureDetails? LastFailure
    {
        get
        {
            lock (_sync)
            {
                return _lastFailure;
            }
        }
    }

    public IReadOnlyList<string> LogLines
    {
        get
        {
            lock (_sync)
            {
                return _logLines.ToArray();
            }
        }
    }

    public void ClearLogs()
    {
        lock (_sync)
        {
            _logLines.Clear();
        }
    }

    public async Task CleanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryBeginOperation())
        {
            return;
        }

        try
        {
            ResetRunDiagnostics();
            RunnerDefinitionValidator.ThrowIfInvalid(Definition);
        }
        catch (Exception ex)
        {
            SetFailure("Clean failed.", exitCode: null, exception: ex);
            SetStatus(RunnerStatus.Failed);
            throw;
        }

        if (await RunCommandPhaseAsync(
            RunnerStatus.Cleaning,
            "Clean failed.",
            Definition.CustomCommands.Clean,
            cancellationToken))
        {
            ClearFailure();
            SetStatus(RunnerStatus.Stopped);
        }
    }

    public async Task BuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryBeginOperation())
        {
            return;
        }

        try
        {
            ResetRunDiagnostics();
            RunnerDefinitionValidator.ThrowIfInvalid(Definition);
        }
        catch (Exception ex)
        {
            SetFailure("Build failed.", exitCode: null, exception: ex);
            SetStatus(RunnerStatus.Failed);
            throw;
        }

        if (!await RunCommandPhaseAsync(
            RunnerStatus.Restoring,
            "Restore failed.",
            Definition.CustomCommands.Restore,
            cancellationToken))
        {
            return;
        }

        if (await RunCommandPhaseAsync(
            RunnerStatus.Building,
            "Build failed.",
            Definition.CustomCommands.Build,
            cancellationToken))
        {
            ClearFailure();
            SetStatus(RunnerStatus.Stopped);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryBeginOperation())
        {
            return;
        }

        try
        {
            ResetRunDiagnostics();
            RunnerDefinitionValidator.ThrowIfInvalid(Definition);
        }
        catch (Exception ex)
        {
            SetFailure("Run failed.", exitCode: null, exception: ex);
            SetStatus(RunnerStatus.Failed);
            throw;
        }

        if (Definition.CleanBeforeRestore
            && !await RunCommandPhaseAsync(
                RunnerStatus.Cleaning,
                "Clean failed.",
                Definition.CustomCommands.Clean,
                cancellationToken))
        {
            return;
        }

        if (!await RunCommandPhaseAsync(
            RunnerStatus.Restoring,
            "Restore failed.",
            Definition.CustomCommands.Restore,
            cancellationToken))
        {
            return;
        }

        if (!await RunCommandPhaseAsync(
            RunnerStatus.Building,
            "Build failed.",
            Definition.CustomCommands.Build,
            cancellationToken))
        {
            return;
        }

        if (await RunCommandPhaseAsync(
            RunnerStatus.Running,
            "Run failed.",
            Definition.CustomCommands.Run,
            cancellationToken))
        {
            ClearFailure();
            SetStatus(RunnerStatus.Stopped);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;
        var alreadyStopped = false;

        lock (_sync)
        {
            process = _process;

            if (process is null || process.HasExited)
            {
                _process = null;
                alreadyStopped = true;
            }
        }

        if (alreadyStopped)
        {
            SetStatus(RunnerStatus.Stopped);
            return;
        }

        if (process is null)
        {
            SetStatus(RunnerStatus.Stopped);
            return;
        }

        SetStatus(RunnerStatus.Stopping);

        try
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Process already exited between checks.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetFailure("Stop failed.", exitCode: null, exception: ex);
            SetStatus(RunnerStatus.Failed);
            throw;
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_process, process))
                {
                    _process = null;
                }
            }

            if (Status == RunnerStatus.Stopping)
            {
                SetStatus(RunnerStatus.Stopped);
            }

            process.Dispose();
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        await StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Status is RunnerStatus.Cleaning
            or RunnerStatus.Restoring
            or RunnerStatus.Building
            or RunnerStatus.Running
            or RunnerStatus.Stopping)
        {
            await StopAsync();
        }

        lock (_sync)
        {
            _process?.Dispose();
            _process = null;
        }
    }

    private bool TryBeginOperation()
    {
        lock (_sync)
        {
            if (_status is RunnerStatus.Cleaning
                or RunnerStatus.Restoring
                or RunnerStatus.Building
                or RunnerStatus.Starting
                or RunnerStatus.Running
                or RunnerStatus.Stopping)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> RunCommandPhaseAsync(
        RunnerStatus status,
        string failureReason,
        string command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return true;
        }

        Process? process = null;

        try
        {
            process = new Process
            {
                StartInfo = CreateShellStartInfo(Definition, command),
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, args) => AddLogLine("out", args.Data);
            process.ErrorDataReceived += (_, args) => AddLogLine("err", args.Data);

            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("The custom command process did not start.");
            }

            lock (_sync)
            {
                _process = process;
            }

            SetStatus(status);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);

            var exitCode = process.ExitCode;

            lock (_sync)
            {
                if (ReferenceEquals(_process, process))
                {
                    _process = null;
                }
            }

            if (Status is RunnerStatus.Stopping or RunnerStatus.Stopped)
            {
                return false;
            }

            if (exitCode == 0)
            {
                return true;
            }

            SetFailure(failureReason, exitCode, exception: null);
            SetStatus(RunnerStatus.Failed);
            throw new InvalidOperationException($"{failureReason} Command exited with code {exitCode}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (Status == RunnerStatus.Failed)
            {
                throw;
            }

            if (Status is RunnerStatus.Stopping or RunnerStatus.Stopped)
            {
                return false;
            }

            SetFailure(failureReason, exitCode: null, exception: ex);
            SetStatus(RunnerStatus.Failed);
            throw;
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            SetStatus(RunnerStatus.Stopped);
            throw;
        }
        finally
        {
            if (process is not null)
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_process, process))
                    {
                        _process = null;
                    }
                }

                process.Dispose();
            }
        }
    }

    private static ProcessStartInfo CreateShellStartInfo(
        RunnerDefinition definition,
        string command)
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = definition.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            }
            : new ProcessStartInfo
            {
                FileName = "/bin/sh",
                WorkingDirectory = definition.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/s");
            startInfo.ArgumentList.Add("/c");
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
        }

        startInfo.ArgumentList.Add(command);

        foreach (var (key, value) in definition.EnvironmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        return startInfo;
    }

    private static void TryKillProcess(Process? process)
    {
        try
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    private void AddLogLine(string stream, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] [{stream}] {text}";

        lock (_sync)
        {
            _logLines.Add(line);

            if (_logLines.Count > MaxLogLines)
            {
                _logLines.RemoveAt(0);
            }
        }
    }

    private void ResetRunDiagnostics()
    {
        lock (_sync)
        {
            _logLines.Clear();
            _lastFailure = null;
        }
    }

    private void ClearFailure()
    {
        lock (_sync)
        {
            _lastFailure = null;
        }
    }

    private void SetFailure(string reason, int? exitCode, Exception? exception)
    {
        lock (_sync)
        {
            _lastFailure = new RunnerFailureDetails(
                DateTimeOffset.Now,
                reason,
                exitCode,
                exception?.Message,
                _logLines.TakeLast(MaxDiagnosticLines).ToArray());
        }
    }

    private void SetStatus(RunnerStatus status)
    {
        var changed = false;

        lock (_sync)
        {
            if (_status != status)
            {
                _status = status;
                changed = true;
            }
        }

        if (changed)
        {
            StatusChanged?.Invoke(this, status);
        }
    }
}
