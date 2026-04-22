using System.Windows;
using System.Windows.Controls;
using AnalyticsVM = Fluxo.ViewModels.Shell.Main.AnalyticsVM;

namespace Fluxo.Views.Shell.Main;

public partial class Analytics : UserControl
{
    private readonly AnalyticsVM _viewModel;
    private bool _hasLoadedData;

    public Analytics(AnalyticsVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoadedData)
            return;

        await _viewModel.LoadAsync();
        _hasLoadedData = true;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        _viewModel.Dispose();
    }
}
