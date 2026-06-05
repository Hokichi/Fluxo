using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main.Pages;

public partial class Calendar : UserControl
{
    private readonly CalendarVM _viewModel;
    private readonly SemaphoreSlim _openPreparationGate = new(1, 1);

    public Calendar(CalendarVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Unloaded += OnUnloaded;
    }

    public async Task PrepareForOpenAsync(CancellationToken cancellationToken = default)
    {
        await _openPreparationGate.WaitAsync(cancellationToken);
        try
        {
            await _viewModel.LoadAsync(cancellationToken);
        }
        finally
        {
            _openPreparationGate.Release();
        }
    }

    private void OnCalendarMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        _viewModel.ScrollCalendarRowsCommand.Execute(e.Delta < 0 ? 1 : -1);
        e.Handled = true;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        _viewModel.Dispose();
    }
}
