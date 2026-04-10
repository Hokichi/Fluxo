using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.ViewModels.Entities;
using Fluxo.Views.Shell;

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
        if (e.ClickCount != 2 || DataContext is not SpendingSourceVM spendingSource)
            return;

        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.OpenSpendingSourceDetailPopup(spendingSource);
    }
}
