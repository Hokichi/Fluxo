using System.Windows;
using System.Windows.Controls;
using AnalyticsVM = Fluxo.ViewModels.Shell.Main.AnalyticsVM;

namespace Fluxo.Views.Shell.Main.Pages;

public partial class Analytics : UserControl
{
    private readonly AnalyticsVM _viewModel;
    private readonly SemaphoreSlim _openPreparationGate = new(1, 1);

    public Analytics(AnalyticsVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Unloaded += OnUnloaded;
    }

    public void ApplyOpenRange(DateTime from, DateTime to)
    {
        _viewModel.ApplyExternalDateRange(from, to, refresh: false);
    }

    public async Task PrepareForOpenAsync(bool showInternalToast, CancellationToken cancellationToken = default)
    {
        await _openPreparationGate.WaitAsync(cancellationToken);
        try
        {
            await _viewModel.RefreshForOpenAsync(showInternalToast, cancellationToken);
        }
        finally
        {
            _openPreparationGate.Release();
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        _viewModel.Dispose();
    }
}
