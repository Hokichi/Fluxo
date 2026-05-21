using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Fluxo.Core.Enums;
using Fluxo.Services.Logging;

namespace Fluxo.Views.Popups;

public partial class ToastPopup : BasePopup
{
    private readonly Func<Task> _work;
    private bool _executionStarted;
    private bool _executionCompleted;

    public ToastPopup(string message, Func<Task> work, NotificationSeverity severity = NotificationSeverity.Info)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Toast message cannot be empty.", nameof(message));

        ArgumentNullException.ThrowIfNull(work);

        InitializeComponent();

        MessageTextBlock.Text = message;
        ShowCloseButton = false;
        _work = work;
    }

    public Exception? ExecutionException { get; private set; }

    protected override void OnFadeInCompleted()
    {
        base.OnFadeInCompleted();

        if (_executionStarted)
            return;

        _executionStarted = true;
        _ = RunWrappedWorkAsync();
    }

    protected override void OnCloseButtonClick()
    {
        if (_executionCompleted)
            base.OnCloseButtonClick();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_executionCompleted)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    private async Task RunWrappedWorkAsync()
    {
        try
        {
            await _work().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            FluxoLogManager.LogError(ex, "Failed to execute toast popup background work.");
            ExecutionException = ex;
        }
        finally
        {
            _executionCompleted = true;
            await Dispatcher.InvokeAsync(Close);
        }
    }
}