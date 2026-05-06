using System.IO.Pipes;
using System.Threading;

namespace Fluxo.Infrastructure.SingleInstance;

public sealed class SingleInstanceCoordinator : ISingleInstanceCoordinator
{
    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly object _sync = new();
    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private bool _isDisposed;
    private bool _isPrimary;

    public SingleInstanceCoordinator(string appKey = "Fluxo")
    {
        _mutexName = $@"Local\{appKey}.SingleInstance";
        _pipeName = $"{appKey}.Activate";
    }

    public bool TryEnterAsPrimary(Action onActivationRequested)
    {
        ArgumentNullException.ThrowIfNull(onActivationRequested);

        lock (_sync)
        {
            ThrowIfDisposed();

            if (_isPrimary)
                return true;

            _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
            if (!createdNew)
            {
                TrySignalExistingInstance();
                return false;
            }

            _listenerCts = new CancellationTokenSource();
            _listenerTask = ListenAsync(onActivationRequested, _listenerCts.Token);
            _isPrimary = true;
            return true;
        }
    }

    private async Task ListenAsync(Action onActivationRequested, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                var signalBuffer = new byte[1];
                _ = await server.ReadAsync(signalBuffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);

                onActivationRequested();
            }
        }
        catch (OperationCanceledException)
        {
            // Listener cancellation is expected during shutdown.
        }
        catch (ObjectDisposedException)
        {
            // Listener resources may already be disposed during shutdown.
        }
    }

    private void TrySignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(timeout: 250);
            client.WriteByte(1);
            client.Flush();
        }
        catch
        {
            // Activation signal is best-effort only.
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _listenerCts?.Cancel();
        }

        try
        {
            _listenerTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Listener cancellation is expected.
        }
        finally
        {
            lock (_sync)
            {
                _listenerTask = null;
                _listenerCts?.Dispose();
                _listenerCts = null;

                if (_mutex is not null)
                {
                    if (_isPrimary)
                    {
                        try
                        {
                            _mutex.ReleaseMutex();
                        }
                        catch (ApplicationException)
                        {
                            // Mutex was not owned on this thread.
                        }
                    }

                    _mutex.Dispose();
                    _mutex = null;
                }
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }
}
