using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Runner.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly object _handlerGate = new();
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly Queue<string[]> _pendingRequests = [];
    private Func<string[], Task>? _launchRequestHandler;
    private Task? _listenTask;
    private bool _disposed;
    private bool _ownsMutex;

    private SingleInstanceService(Mutex mutex, bool ownsMutex, string pipeName)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
        _pipeName = pipeName;
    }

    public bool IsPrimaryInstance => _ownsMutex;

    public static SingleInstanceService Acquire(string instanceName)
    {
        var mutex = new Mutex(false, instanceName);
        bool ownsMutex;

        try
        {
            ownsMutex = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            ownsMutex = true;
        }

        return new SingleInstanceService(mutex, ownsMutex, instanceName);
    }

    public void StartListening()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsPrimaryInstance || _listenTask is not null)
        {
            return;
        }

        _listenTask = ListenAsync(_cancellationTokenSource.Token);
    }

    public void RegisterLaunchRequestHandler(Func<string[], Task> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string[][] pendingRequests;

        lock (_handlerGate)
        {
            _launchRequestHandler = handler;
            pendingRequests = _pendingRequests.ToArray();
            _pendingRequests.Clear();
        }

        foreach (var pendingRequest in pendingRequests)
        {
            _ = DispatchLaunchRequestAsync(pendingRequest);
        }
    }

    public async Task<bool> SendLaunchRequestAsync(string[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var cancellationTokenSource = new CancellationTokenSource(ConnectTimeout);

        try
        {
            await using var client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(cancellationTokenSource.Token);

            var payload = JsonSerializer.Serialize(args);
            await using var writer = new StreamWriter(client, Encoding.UTF8);
            await writer.WriteAsync(payload.AsMemory(), cancellationTokenSource.Token);
            await writer.FlushAsync(cancellationTokenSource.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _mutex.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                var args = JsonSerializer.Deserialize<string[]>(payload) ?? [];
                await DispatchLaunchRequestAsync(args);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    private Task DispatchLaunchRequestAsync(string[] args)
    {
        Func<string[], Task>? handler;

        lock (_handlerGate)
        {
            handler = _launchRequestHandler;

            if (handler is null)
            {
                _pendingRequests.Enqueue(args);
                return Task.CompletedTask;
            }
        }

        return handler(args);
    }
}
