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

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_status is RunnerStatus.Starting or RunnerStatus.Running)
            {
                return Task.CompletedTask;
            }
        }

        try
        {
            ResetRunDiagnostics();
            RunnerDefinitionValidator.ThrowIfInvalid(Definition);
            SetStatus(RunnerStatus.Starting);

            var startInfo = CreateStartInfo(Definition);
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

            return Task.CompletedTask;
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
        if (Status is RunnerStatus.Running or RunnerStatus.Starting or RunnerStatus.Stopping)
        {
            await StopAsync();
        }

        lock (_sync)
        {
            _process?.Dispose();
            _process = null;
        }
    }

    private static ProcessStartInfo CreateStartInfo(RunnerDefinition definition)
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

        startInfo.ArgumentList.Add("run");

        if (!string.IsNullOrWhiteSpace(definition.Command))
        {
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(definition.Command);
        }

        foreach (var argument in CommandLineTokenizer.Split(definition.Arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in definition.EnvironmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        return startInfo;
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
