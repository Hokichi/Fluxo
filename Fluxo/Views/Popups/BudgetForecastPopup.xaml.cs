using System.Windows;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class BudgetForecastPopup : BasePopup
{
    private readonly BudgetForecastVM _viewModel;

    public BudgetForecastPopup(BudgetForecastVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.LoadAsync();
    }

    private void OnAddEventClick(object sender, RoutedEventArgs e)
    {
        _viewModel.AddEvent();
    }

    private void OnDeleteEventClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BudgetForecastEventRowVM row)
            _viewModel.DeleteEvent(row);
    }
}
