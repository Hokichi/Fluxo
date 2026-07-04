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
    private readonly Stack<LogMemoryEntry> _redoStack = [];
    private readonly Stack<LogMemoryEntry> _undoStack = [];
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

    public bool CanUndo => PeekEligible(_undoStack, false) is not null;

    public bool CanRedo => PeekEligible(_redoStack, true) is not null;

    public event EventHandler? StateChanged;

    public Task<bool> UndoAsync(CancellationToken cancellationToken = default)
    {
        return ApplyStackAsync(_undoStack, _redoStack, LogMemoryApplyDirection.Revert, cancellationToken);
    }

    public Task<bool> RedoAsync(CancellationToken cancellationToken = default)
    {
        return ApplyStackAsync(_redoStack, _undoStack, LogMemoryApplyDirection.Reapply, cancellationToken);
    }

    public async Task<bool> RevertToAsync(
        LogMemoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_isExecuting || !HistoryEntries.Contains(entry) || entry.IsReverted)
            return false;

        var entries = HistoryEntries
            .Skip(HistoryEntries.IndexOf(entry) + 1)
            .Where(candidate => !candidate.IsReverted)
            .Reverse()
            .ToArray();
        if (entries.Length == 0)
            return false;

        _isExecuting = true;
        try
        {
            await _dataOperationRunner.RunInTransactionAsync(
                "revert history range",
                async (scope, ct) =>
                {
                    foreach (var candidate in entries)
                        await candidate.Action.RevertAsync(scope.UnitOfWork, ct);
                },
                cancellationToken);

            foreach (var candidate in entries)
            {
                candidate.IsReverted = true;
                _redoStack.Push(candidate);
                _messenger.Send(new LogMemoryActionAppliedMessage(
                    candidate.Action,
                    LogMemoryApplyDirection.Revert));
            }

            RefreshState();
            await _reloadCurrentDataAsync();
            return true;
        }
        finally
        {
            _isExecuting = false;
        }
    }

    public async Task<bool> ToggleAsync(LogMemoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_isExecuting || !HistoryEntries.Contains(entry))
            return false;

        var direction = entry.IsReverted
            ? LogMemoryApplyDirection.Reapply
            : LogMemoryApplyDirection.Revert;
        var source = direction == LogMemoryApplyDirection.Revert ? _undoStack : _redoStack;
        var destination = direction == LogMemoryApplyDirection.Revert ? _redoStack : _undoStack;
        var isCurrent = ReferenceEquals(
            PeekEligible(source, direction == LogMemoryApplyDirection.Reapply),
            entry);

        _isExecuting = true;
        try
        {
            await ApplyAsync(entry, direction, cancellationToken);

            if (isCurrent)
            {
                PopThrough(source, entry);
                destination.Push(entry);
            }

            entry.IsReverted = direction == LogMemoryApplyDirection.Revert;
            _messenger.Send(new LogMemoryActionAppliedMessage(entry.Action, direction));
            RefreshState();
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
        if (_isExecuting)
            return;

        var entry = new LogMemoryEntry(action);
        HistoryEntries.Add(entry);
        _undoStack.Push(entry);
        _redoStack.Clear();
        RefreshState();
    }

    private async Task<bool> ApplyStackAsync(
        Stack<LogMemoryEntry> source,
        Stack<LogMemoryEntry> destination,
        LogMemoryApplyDirection direction,
        CancellationToken cancellationToken)
    {
        if (_isExecuting)
            return false;

        var entry = PopEligible(source, direction == LogMemoryApplyDirection.Reapply);
        if (entry is null)
            return false;

        _isExecuting = true;
        try
        {
            try
            {
                await ApplyAsync(entry, direction, cancellationToken);
            }
            catch
            {
                source.Push(entry);
                throw;
            }

            entry.IsReverted = direction == LogMemoryApplyDirection.Revert;
            destination.Push(entry);
            _messenger.Send(new LogMemoryActionAppliedMessage(entry.Action, direction));
            RefreshState();
            await _reloadCurrentDataAsync();
            return true;
        }
        finally
        {
            _isExecuting = false;
        }
    }

    private Task ApplyAsync(
        LogMemoryEntry entry,
        LogMemoryApplyDirection direction,
        CancellationToken cancellationToken)
    {
        return _dataOperationRunner.RunAsync(
            direction == LogMemoryApplyDirection.Revert ? "revert history action" : "reapply history action",
            (scope, ct) => direction == LogMemoryApplyDirection.Revert
                ? entry.Action.RevertAsync(scope.UnitOfWork, ct)
                : entry.Action.ReapplyAsync(scope.UnitOfWork, ct),
            cancellationToken);
    }

    private void RefreshState()
    {
        var hasNewerActiveEntry = false;
        for (var index = HistoryEntries.Count - 1; index >= 0; index--)
        {
            var entry = HistoryEntries[index];
            entry.CanRevertToHere = !entry.IsReverted && hasNewerActiveEntry;
            if (!entry.IsReverted)
                hasNewerActiveEntry = true;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static LogMemoryEntry? PeekEligible(Stack<LogMemoryEntry> stack, bool isReverted)
    {
        return stack.FirstOrDefault(entry => entry.IsReverted == isReverted);
    }

    private static LogMemoryEntry? PopEligible(Stack<LogMemoryEntry> stack, bool isReverted)
    {
        while (stack.TryPop(out var entry))
        {
            if (entry.IsReverted == isReverted)
                return entry;
        }

        return null;
    }

    private static void PopThrough(Stack<LogMemoryEntry> stack, LogMemoryEntry expected)
    {
        while (stack.TryPop(out var entry))
        {
            if (ReferenceEquals(entry, expected))
                return;
        }
    }
}
