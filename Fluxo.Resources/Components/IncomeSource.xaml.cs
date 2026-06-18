using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Resources.Infrastructure;

namespace Fluxo.Resources.Components;

/// <summary>
///     Interaction logic for IncomeSource.xaml
/// </summary>
public partial class IncomeSource : UserControl
{
    public IncomeSource()
    {
        InitializeComponent();
    }

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            ToggleAccountFilter();
            return;
        }

        if (e.ClickCount != 2)
            return;

        OpenAccountDetail();
    }

    private void OnRootPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is null)
            return;

        SourceActionsPopup.IsOpen = true;
        e.Handled = true;
    }

    private void OnViewActionClick(object sender, RoutedEventArgs e)
    {
        CloseActionsPopup();
        OpenAccountDetail();
    }

    private async void OnDeleteActionClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsyncAction("ExecuteDeleteAccountActionAsync");
    }

    private async void OnHideActionClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsyncAction("ExecuteHideAccountActionAsync");
    }

    private async void OnDisableActionClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsyncAction("ExecuteDisableAccountActionAsync");
    }

    private void OnTransferActionClick(object sender, RoutedEventArgs e)
    {
        CloseActionsPopup();

        if (DataContext is null)
            return;

        WindowMethodInvoker.TryInvoke(this, "OpenTransferFundsPopup", DataContext);
    }

    private void OnReconciliationActionClick(object sender, RoutedEventArgs e)
    {
        CloseActionsPopup();

        if (DataContext is null)
            return;

        WindowMethodInvoker.TryInvoke(this, "OpenAccountReconciliationPopup", DataContext);
    }

    private async Task ExecuteAsyncAction(string methodName)
    {
        CloseActionsPopup();

        if (DataContext is null)
            return;

        await WindowMethodInvoker.TryInvokeAsync(this, methodName, DataContext);
    }

    private void OpenAccountDetail()
    {
        if (DataContext is null)
            return;

        WindowMethodInvoker.TryInvoke(this, "OpenAccountDetailPopup", DataContext);
    }

    private void ToggleAccountFilter()
    {
        if (DataContext is null)
            return;

        WindowMethodInvoker.TryInvoke(this, "ToggleAccountFilter", DataContext);
    }

    private void CloseActionsPopup()
    {
        SourceActionsPopup.IsOpen = false;
    }
}
