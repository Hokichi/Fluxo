using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;

namespace Fluxo.Infrastructure.SingleInstance;

public sealed class SingleInstanceCoordinator : ISingleInstanceCoordinator
{
    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly object _sync = new();
    private static readonly TimeSpan ListenerErrorBackoff = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan SignalRetryWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SignalRetryDelay = TimeSpan.FromMilliseconds(120);
    private const int ConnectAttemptTimeoutMilliseconds = 180;
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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                var signalBuffer = new byte[1];
                var bytesRead = await server.ReadAsync(signalBuffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);

                if (bytesRead > 0)
                    onActivationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Listener cancellation is expected during shutdown.
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                // Listener resources may already be disposed during shutdown.
                break;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Transient pipe failures should not permanently disable activation handling.
                try
                {
                    await Task.Delay(ListenerErrorBackoff, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void TrySignalExistingInstance()
    {
        var retryTimer = Stopwatch.StartNew();
        while (retryTimer.Elapsed < SignalRetryWindow)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                client.Connect(timeout: ConnectAttemptTimeoutMilliseconds);
                client.WriteByte(1);
                client.Flush();
                return;
            }
            catch
            {
                // Activation signal is best-effort only.
            }

            Thread.Sleep(SignalRetryDelay);
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
