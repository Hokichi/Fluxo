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
            ToggleSpendingSourceFilter();
            return;
        }

        if (e.ClickCount != 2)
            return;

        OpenSpendingSourceDetail();
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
        OpenSpendingSourceDetail();
    }

    private async void OnDeleteActionClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsyncAction("ExecuteDeleteSpendingSourceActionAsync");
    }

    private async void OnHideActionClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsyncAction("ExecuteHideSpendingSourceActionAsync");
    }

    private async void OnDisableActionClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsyncAction("ExecuteDisableSpendingSourceActionAsync");
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

    private void OpenSpendingSourceDetail()
    {
        if (DataContext is null)
            return;

        WindowMethodInvoker.TryInvoke(this, "OpenSpendingSourceDetailPopup", DataContext);
    }

    private void ToggleSpendingSourceFilter()
    {
        if (DataContext is null)
            return;

        WindowMethodInvoker.TryInvoke(this, "ToggleSpendingSourceFilter", DataContext);
    }

    private void CloseActionsPopup()
    {
        SourceActionsPopup.IsOpen = false;
    }
}
