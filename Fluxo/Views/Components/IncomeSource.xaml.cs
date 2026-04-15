using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.ViewModels.Entities;
using Fluxo.Views.Shell.Main;

namespace Fluxo.Views.Components;

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
        if (e.ClickCount != 2)
            return;

        OpenSpendingSourceDetail();
    }

    private void OnRootPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not SpendingSourceVM)
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
        await ExecuteAsyncAction((mainWindow, spendingSource) =>
            mainWindow.ExecuteDeleteSpendingSourceActionAsync(spendingSource));
    }

    private async void OnHideActionClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsyncAction((mainWindow, spendingSource) =>
            mainWindow.ExecuteHideSpendingSourceActionAsync(spendingSource));
    }

    private async void OnDisableActionClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsyncAction((mainWindow, spendingSource) =>
            mainWindow.ExecuteDisableSpendingSourceActionAsync(spendingSource));
    }

    private void OnTransferActionClick(object sender, RoutedEventArgs e)
    {
        CloseActionsPopup();

        if (DataContext is not SpendingSourceVM spendingSource)
            return;

        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.OpenTransferFundsPopup(spendingSource);
    }

    private async Task ExecuteAsyncAction(Func<MainWindow, SpendingSourceVM, Task> action)
    {
        CloseActionsPopup();

        if (DataContext is not SpendingSourceVM spendingSource)
            return;

        if (Window.GetWindow(this) is not MainWindow mainWindow)
            return;

        await action(mainWindow, spendingSource);
    }

    private void OpenSpendingSourceDetail()
    {
        if (DataContext is not SpendingSourceVM spendingSource)
            return;

        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.OpenSpendingSourceDetailPopup(spendingSource);
    }

    private void CloseActionsPopup()
    {
        SourceActionsPopup.IsOpen = false;
    }
}
