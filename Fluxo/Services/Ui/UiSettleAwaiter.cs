using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Fluxo.Services.Ui;

public sealed class UiSettleAwaiter : IUiSettleAwaiter
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(3);

    public async Task WaitForUiReadyAsync(Window? owner = null, CancellationToken cancellationToken = default)
    {
        var dispatcher = ResolveDispatcher(owner);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(SettleTimeout);

        try
        {
            await PumpPriorityAsync(dispatcher, DispatcherPriority.DataBind, timeoutCts.Token);
            await PumpPriorityAsync(dispatcher, DispatcherPriority.Render, timeoutCts.Token);
            await WaitForNextRenderAsync(dispatcher, timeoutCts.Token);
            await PumpPriorityAsync(dispatcher, DispatcherPriority.ContextIdle, timeoutCts.Token);
            await PumpPriorityAsync(dispatcher, DispatcherPriority.ApplicationIdle, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Best-effort settle gate. Timeout should not block flow completion forever.
        }
    }

    private static Dispatcher ResolveDispatcher(Window? owner)
    {
        if (owner is not null)
            return owner.Dispatcher;

        if (Application.Current?.MainWindow is not null)
            return Application.Current.MainWindow.Dispatcher;

        if (Application.Current?.Dispatcher is not null)
            return Application.Current.Dispatcher;

        return Dispatcher.CurrentDispatcher;
    }

    private static Task PumpPriorityAsync(Dispatcher dispatcher, DispatcherPriority priority,
        CancellationToken cancellationToken)
    {
        return dispatcher.InvokeAsync(static () => { }, priority, cancellationToken).Task;
    }

    private static Task WaitForNextRenderAsync(Dispatcher dispatcher, CancellationToken cancellationToken)
    {
        return dispatcher.InvokeAsync(() => WaitForNextRenderCoreAsync(cancellationToken), DispatcherPriority.Render,
            cancellationToken).Task.Unwrap();
    }

    private static Task WaitForNextRenderCoreAsync(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? renderHandler = null;
        CancellationTokenRegistration cancellationRegistration = default;

        renderHandler = (_, _) =>
        {
            CompositionTarget.Rendering -= renderHandler;
            cancellationRegistration.Dispose();
            completion.TrySetResult();
        };

        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(() =>
            {
                CompositionTarget.Rendering -= renderHandler;
                completion.TrySetCanceled(cancellationToken);
            });
        }

        CompositionTarget.Rendering += renderHandler;
        return completion.Task;
    }
}
