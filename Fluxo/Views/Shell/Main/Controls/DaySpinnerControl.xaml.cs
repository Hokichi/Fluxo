using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Views.Shell.Main.Controls;

public partial class DaySpinnerControl : UserControl
{
    private bool _isUserSelectionChangePending;

    public DaySpinnerControl()
    {
        InitializeComponent();
    }

    private async void OnNavigateSpinnerBackClick(object sender, RoutedEventArgs e)
    {
        _isUserSelectionChangePending = false;

        if (DataContext is not DaySpinnerVM viewModel)
            return;

        await viewModel.NavigateSpinnerBackFromUserAsync(Window.GetWindow(this));
    }

    private async void OnNavigateSpinnerForwardClick(object sender, RoutedEventArgs e)
    {
        _isUserSelectionChangePending = false;

        if (DataContext is not DaySpinnerVM viewModel)
            return;

        await viewModel.NavigateSpinnerForwardFromUserAsync(Window.GetWindow(this));
    }

    private void OnSpinnerListPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isUserSelectionChangePending = true;
    }

    private void OnSpinnerListPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isUserSelectionChangePending = false;
    }

    private void OnSpinnerListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsSelectionNavigationKey(e.Key))
            _isUserSelectionChangePending = true;
    }

    private void OnSpinnerListPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (IsSelectionNavigationKey(e.Key))
            _isUserSelectionChangePending = false;
    }

    private async void OnSelectedDayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUserSelectionChangePending)
            return;

        _isUserSelectionChangePending = false;

        if (DataContext is not DaySpinnerVM viewModel)
            return;

        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not DayOfWeekVM selectedDay)
            return;

        await viewModel.SelectDayFromUserAsync(selectedDay, Window.GetWindow(this));
    }

    private static bool IsSelectionNavigationKey(Key key)
    {
        return key is Key.Left or
            Key.Right or
            Key.Up or
            Key.Down or
            Key.Home or
            Key.End or
            Key.PageUp or
            Key.PageDown or
            Key.Enter or
            Key.Space;
    }
}
