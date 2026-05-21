using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using Fluxo.Services.Updates;

namespace Fluxo.Views.Popups;

public partial class DownloadUpdatePopup : BasePopup
{
    private readonly Func<IProgress<double>, CancellationToken, Task<string>> _downloadInstallerAsync;
    private readonly CancellationTokenSource _downloadCancellation = new();
    private bool _downloadStarted;
    private bool _downloadCompleted;
    private bool _isCancellationRequested;

    public DownloadUpdatePopup(
        AppUpdateCheckResult update,
        Func<IProgress<double>, CancellationToken, Task<string>> downloadInstallerAsync)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(downloadInstallerAsync);

        InitializeComponent();

        _downloadInstallerAsync = downloadInstallerAsync;
        VersionTextBlock.Text = $"Downloading version {NormalizeVersion(update.LatestVersion)}";
    }

    public string? InstallerPath { get; private set; }

    public bool IsCanceled { get; private set; }

    public Exception? ExecutionException { get; private set; }

    protected override void OnFadeInCompleted()
    {
        base.OnFadeInCompleted();

        if (_downloadStarted)
            return;

        _downloadStarted = true;
        _ = RunDownloadAsync();
    }

    protected override void OnCloseButtonClick()
    {
        RequestCancellation();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_downloadCompleted)
        {
            e.Cancel = true;
            RequestCancellation();
            return;
        }

        base.OnClosing(e);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        RequestCancellation();
    }

    private async Task RunDownloadAsync()
    {
        try
        {
            var progress = new Progress<double>(UpdateProgress);
            InstallerPath = await _downloadInstallerAsync(progress, _downloadCancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (_isCancellationRequested)
        {
            IsCanceled = true;
        }
        catch (Exception exception)
        {
            ExecutionException = exception;
        }
        finally
        {
            _downloadCompleted = true;
            await Dispatcher.InvokeAsync(Close);
            _downloadCancellation.Dispose();
        }
    }

    private void RequestCancellation()
    {
        if (_downloadCompleted || _isCancellationRequested)
            return;

        _isCancellationRequested = true;
        _downloadCancellation.Cancel();
    }

    private void UpdateProgress(double value)
    {
        var percentage = (int)Math.Round(Math.Clamp(value, 0d, 100d), MidpointRounding.AwayFromZero);
        ProgressTextBlock.Text = $"{percentage}%";
    }

    private static string NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? "Unknown"
            : version.Trim();
    }

    public void RethrowIfFailed()
    {
        if (ExecutionException is not null)
            ExceptionDispatchInfo.Capture(ExecutionException).Throw();
    }
}
