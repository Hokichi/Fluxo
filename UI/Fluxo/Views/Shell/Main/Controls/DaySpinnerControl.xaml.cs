using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main.Controls;

public partial class DaySpinnerControl : UserControl
{
    public DaySpinnerControl()
    {
        InitializeComponent();
    }

    private async void OnNavigateSpinnerBackClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DaySpinnerVM viewModel)
            return;

        await viewModel.NavigateSpinnerBackFromUserAsync(Window.GetWindow(this));
    }

    private async void OnNavigateSpinnerForwardClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DaySpinnerVM viewModel)
            return;

        await viewModel.NavigateSpinnerForwardFromUserAsync(Window.GetWindow(this));
    }

    private async void OnSelectedDayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not DaySpinnerVM viewModel)
            return;

        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not DayOfWeekVM selectedDay)
            return;

        if (ReferenceEquals(viewModel.SelectedDay, selectedDay))
            return;

        await viewModel.SelectDayFromUserAsync(selectedDay, Window.GetWindow(this));
    }
}
