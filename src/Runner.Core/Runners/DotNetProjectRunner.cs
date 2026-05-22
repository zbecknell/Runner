using System.Diagnostics;

namespace Runner.Core.Runners;

public sealed class DotNetProjectRunner : IRunner
{
    private const int MaxDiagnosticLines = 100;
    private static readonly TimeSpan GracefulStopTimeout = TimeSpan.FromSeconds(2);

    private readonly Lock _sync = new();
    private readonly List<string> _diagnosticTail = [];
    private Process? _process;
    private RunnerFailureDetails? _lastFailure;
    private RunnerStatus _status = RunnerStatus.Stopped;

    public DotNetProjectRunner(RunnerDefinition definition)
    {
        Definition = definition;
        Definition.EnsureId();
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

    public int? ProcessId
    {
        get
        {
            lock (_sync)
            {
                return _process is { HasExited: false } process ? process.Id : null;
            }
        }
    }

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

        if (await RunPhaseAsync(
            RunnerStatus.Cleaning,
            "Clean failed.",
            CreateCleanStartInfo(Definition),
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

        if (!await RunPhaseAsync(
            RunnerStatus.Restoring,
            "Restore failed.",
            CreateRestoreStartInfo(Definition),
            cancellationToken))
        {
            return;
        }

        if (await RunPhaseAsync(
            RunnerStatus.Building,
            "Build failed.",
            CreateBuildStartInfo(Definition),
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
            SetFailure("Start failed.", exitCode: null, exception: ex);
            SetStatus(RunnerStatus.Failed);
            throw;
        }

        if (Definition.CleanBeforeRestore
            && !await RunPhaseAsync(
                RunnerStatus.Cleaning,
                "Clean failed.",
                CreateCleanStartInfo(Definition),
                cancellationToken))
        {
            return;
        }

        if (!await RunPhaseAsync(
            RunnerStatus.Restoring,
            "Restore failed.",
            CreateRestoreStartInfo(Definition),
            cancellationToken))
        {
            return;
        }

        if (!await RunPhaseAsync(
            RunnerStatus.Building,
            "Build failed.",
            CreateBuildStartInfo(Definition),
            cancellationToken))
        {
            return;
        }

        if (Definition.Type == RunnerType.DotNetProjectBuild)
        {
            ClearFailure();
            SetStatus(RunnerStatus.Stopped);
            return;
        }

        ClearDiagnostics();
        SetStatus(RunnerStatus.Starting);

        try
        {
            var startInfo = CreateRunStartInfo(Definition);
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, args) => AddDiagnosticLine("out", args.Data);
            process.ErrorDataReceived += (_, args) => AddDiagnosticLine("err", args.Data);
            process.Exited += (_, _) => HandleProcessExited(process);

            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("The dotnet process did not start.");
            }

            lock (_sync)
            {
                _process = process;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (process.HasExited)
            {
                HandleProcessExited(process);
            }
            else
            {
                SetStatus(RunnerStatus.Running);
            }
        }
        catch (Exception ex)
        {
            SetFailure("Start failed.", exitCode: null, exception: ex);
            SetStatus(RunnerStatus.Failed);
            throw;
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
            TryCloseMainWindow(process);

            if (!await WaitForExitAsync(process, GracefulStopTimeout, cancellationToken))
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
            }
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
            or RunnerStatus.Starting
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

    private async Task<bool> RunPhaseAsync(
        RunnerStatus status,
        string failureReason,
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        ClearDiagnostics();

        Process? process = null;

        try
        {
            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, args) => AddDiagnosticLine("out", args.Data);
            process.ErrorDataReceived += (_, args) => AddDiagnosticLine("err", args.Data);

            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("The dotnet process did not start.");
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
            throw new InvalidOperationException($"{failureReason} dotnet exited with code {exitCode}.");
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

    private static ProcessStartInfo CreateCleanStartInfo(RunnerDefinition definition)
    {
        var startInfo = CreateDotNetStartInfo(definition);
        startInfo.ArgumentList.Add("clean");
        AddProjectTarget(startInfo, definition);
        return startInfo;
    }

    private static ProcessStartInfo CreateRestoreStartInfo(RunnerDefinition definition)
    {
        var startInfo = CreateDotNetStartInfo(definition);
        startInfo.ArgumentList.Add("restore");
        AddProjectTarget(startInfo, definition);
        return startInfo;
    }

    private static ProcessStartInfo CreateBuildStartInfo(RunnerDefinition definition)
    {
        var startInfo = CreateDotNetStartInfo(definition);
        startInfo.ArgumentList.Add("build");
        AddProjectTarget(startInfo, definition);
        startInfo.ArgumentList.Add("--no-restore");

        if (definition.Type == RunnerType.DotNetProjectBuild)
        {
            foreach (var argument in CommandLineTokenizer.Split(definition.Arguments))
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        return startInfo;
    }

    private static ProcessStartInfo CreateRunStartInfo(RunnerDefinition definition)
    {
        var startInfo = CreateDotNetStartInfo(definition);
        startInfo.ArgumentList.Add("run");

        if (!string.IsNullOrWhiteSpace(definition.Command))
        {
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(definition.Command);
        }

        startInfo.ArgumentList.Add("--no-build");

        foreach (var argument in CommandLineTokenizer.Split(definition.Arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static ProcessStartInfo CreateDotNetStartInfo(RunnerDefinition definition)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = definition.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        foreach (var (key, value) in definition.EnvironmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        return startInfo;
    }

    private static void AddProjectTarget(ProcessStartInfo startInfo, RunnerDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.Command))
        {
            startInfo.ArgumentList.Add(definition.Command);
        }
    }

    private void HandleProcessExited(Process process)
    {
        var exitCode = 0;

        try
        {
            exitCode = process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            // The process was disposed while shutdown was already in progress.
        }

        lock (_sync)
        {
            if (ReferenceEquals(_process, process))
            {
                _process = null;
            }
        }

        if (Status == RunnerStatus.Stopping || exitCode == 0)
        {
            ClearFailure();
            SetStatus(RunnerStatus.Stopped);
            return;
        }

        SetFailure("Process exited with a non-zero exit code.", exitCode, exception: null);
        SetStatus(RunnerStatus.Failed);
    }

    private static void TryCloseMainWindow(Process process)
    {
        try
        {
            if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
            {
                process.CloseMainWindow();
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    private static async Task<bool> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private void AddDiagnosticLine(string stream, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] [{stream}] {text}";

        lock (_sync)
        {
            _diagnosticTail.Add(line);

            if (_diagnosticTail.Count > MaxDiagnosticLines)
            {
                _diagnosticTail.RemoveAt(0);
            }
        }
    }

    private void ResetRunDiagnostics()
    {
        lock (_sync)
        {
            _diagnosticTail.Clear();
            _lastFailure = null;
        }
    }

    private void ClearDiagnostics()
    {
        lock (_sync)
        {
            _diagnosticTail.Clear();
        }
    }

    private void ClearFailure()
    {
        lock (_sync)
        {
            _diagnosticTail.Clear();
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
                _diagnosticTail.ToArray());
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
