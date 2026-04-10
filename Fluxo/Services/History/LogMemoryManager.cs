using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Interfaces;
using Fluxo.ViewModels.Messages;
using Fluxo.ViewModels.Shell;

namespace Fluxo.Services.History;

public sealed class LogMemoryManager : IDisposable
{
    private readonly MainVM _mainViewModel;
    private readonly IMessenger _messenger;
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;
    private readonly Stack<ILogMemoryAction> _redoStack = [];
    private readonly Stack<ILogMemoryAction> _undoStack = [];
    private bool _isDisposed;
    private bool _isExecuting;

    public LogMemoryManager(MainVM mainViewModel, Func<IUnitOfWork> unitOfWorkFactory, IMessenger? messenger = null)
    {
        _mainViewModel = mainViewModel;
        _unitOfWorkFactory = unitOfWorkFactory;
        _messenger = messenger ?? WeakReferenceMessenger.Default;

        _messenger.Register<LogMemoryManager, RecordLogMemoryMessage>(this, static (recipient, message) =>
            recipient.Record(message.Value));
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public async Task<bool> UndoAsync(CancellationToken cancellationToken = default)
    {
        if (_isExecuting || _undoStack.Count == 0)
            return false;

        var action = _undoStack.Pop();
        try
        {
            _isExecuting = true;

            await using var unitOfWork = _unitOfWorkFactory();
            await action.UndoAsync(unitOfWork, cancellationToken);
            await _mainViewModel.ReloadCurrentDataAsync();

            _redoStack.Push(action);
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

            await using var unitOfWork = _unitOfWorkFactory();
            await action.RedoAsync(unitOfWork, cancellationToken);
            await _mainViewModel.ReloadCurrentDataAsync();

            _undoStack.Push(action);
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
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _messenger.UnregisterAll(this);
        _isDisposed = true;
    }
}
