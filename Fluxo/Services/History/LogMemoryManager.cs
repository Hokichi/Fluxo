using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using CoreILogMemoryAction = Fluxo.Core.Interfaces.History.ILogMemoryAction;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Resources.Resources.Messages;

namespace Fluxo.Services.History;

public sealed class LogMemoryManager : IDisposable
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly Func<Task> _reloadCurrentDataAsync;
    private readonly IMessenger _messenger;
    private bool _isDisposed;
    private bool _isExecuting;

    public LogMemoryManager(
        IDataOperationRunner dataOperationRunner,
        Func<Task> reloadCurrentDataAsync,
        IMessenger? messenger = null)
    {
        _dataOperationRunner = dataOperationRunner;
        _reloadCurrentDataAsync = reloadCurrentDataAsync;
        _messenger = messenger ?? WeakReferenceMessenger.Default;

        _messenger.Register<LogMemoryManager, RecordLogMemoryMessage>(this, static (recipient, message) =>
            recipient.Record(message.Value));
    }

    public ObservableCollection<LogMemoryEntry> HistoryEntries { get; } = [];

    public async Task<bool> ToggleAsync(LogMemoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_isExecuting || !HistoryEntries.Contains(entry))
            return false;

        _isExecuting = true;
        try
        {
            var direction = entry.IsReverted
                ? LogMemoryApplyDirection.Reapply
                : LogMemoryApplyDirection.Revert;

            await _dataOperationRunner.RunAsync(
                direction == LogMemoryApplyDirection.Revert
                    ? "revert history action"
                    : "reapply history action",
                async (scope, ct) =>
                {
                    if (direction == LogMemoryApplyDirection.Revert)
                        await entry.Action.RevertAsync(scope.UnitOfWork, ct);
                    else
                        await entry.Action.ReapplyAsync(scope.UnitOfWork, ct);
                },
                cancellationToken);

            entry.IsReverted = direction == LogMemoryApplyDirection.Revert;
            _messenger.Send(new LogMemoryActionAppliedMessage(entry.Action, direction));
            await _reloadCurrentDataAsync();
            return true;
        }
        finally
        {
            _isExecuting = false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _messenger.UnregisterAll(this);
        _isDisposed = true;
    }

    private void Record(CoreILogMemoryAction action)
    {
        if (!_isExecuting)
            HistoryEntries.Add(new LogMemoryEntry(action));
    }
}
