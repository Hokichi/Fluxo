using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Resources.Messages;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo.Services.History;

public sealed class LogMemoryManager : IDisposable
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly MainVM _mainViewModel;
    private readonly IMessenger _messenger;
    private readonly Stack<ILogMemoryAction> _redoStack = [];
    private readonly Stack<ILogMemoryAction> _undoStack = [];
    private bool _isDisposed;
    private bool _isExecuting;

    public LogMemoryManager(MainVM mainViewModel, IDataOperationRunner dataOperationRunner, IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _dataOperationRunner = dataOperationRunner;
        _messenger = messenger ?? WeakReferenceMessenger.Default;

        _messenger.Register<LogMemoryManager, RecordLogMemoryMessage>(this, static (recipient, message) =>
            recipient.Record(message.Value));
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _messenger.UnregisterAll(this);
        _isDisposed = true;
    }

    public event EventHandler? StateChanged;

    public async Task<bool> UndoAsync(CancellationToken cancellationToken = default)
    {
        if (_isExecuting || _undoStack.Count == 0)
            return false;

        var action = _undoStack.Pop();
        try
        {
            _isExecuting = true;

            await _dataOperationRunner.RunAsync(async (scope, ct) =>
            {
                await action.UndoAsync(scope.UnitOfWork, ct);
            }, cancellationToken);
            _messenger.Send(new LogMemoryActionAppliedMessage(action, LogMemoryApplyDirection.Undo));
            await _mainViewModel.ReloadCurrentDataAsync();

            _redoStack.Push(action);
            RaiseStateChanged();
            return true;
        }
        catch
        {
            _undoStack.Push(action);
            throw;
        }
        finally
        {
            _isExecuting = false;
        }
    }

    public async Task<bool> RedoAsync(CancellationToken cancellationToken = default)
    {
        if (_isExecuting || _redoStack.Count == 0)
            return false;

        var action = _redoStack.Pop();
        try
        {
            _isExecuting = true;

            await _dataOperationRunner.RunAsync(async (scope, ct) =>
            {
                await action.RedoAsync(scope.UnitOfWork, ct);
            }, cancellationToken);
            _messenger.Send(new LogMemoryActionAppliedMessage(action, LogMemoryApplyDirection.Redo));
            await _mainViewModel.ReloadCurrentDataAsync();

            _undoStack.Push(action);
            RaiseStateChanged();
            return true;
        }
        catch
        {
            _redoStack.Push(action);
            throw;
        }
        finally
        {
            _isExecuting = false;
        }
    }

    private void Record(ILogMemoryAction action)
    {
        if (_isExecuting)
            return;

        _undoStack.Push(action);
        _redoStack.Clear();
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
